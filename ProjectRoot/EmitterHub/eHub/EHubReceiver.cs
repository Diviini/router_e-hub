using System.IO.Compression;
using System.Net;
using System.Net.Sockets;

namespace EmitterHub.eHub;

/// <summary>
/// Récepteur eHuB pour univers unique : écoute, affiche, retourne les entités
/// </summary>
public class EHubReceiver : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly int _targetUniverse;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Dictionary<ushort, EntityState> _entities;
    private readonly Dictionary<ushort, ushort> _indexToEntityId = new();


    public event Action<Dictionary<ushort, EntityState>>? EntitiesUpdated;

    public int MessagesReceived { get; private set; }
    public int ActiveEntities => _entities.Count;

    public EHubReceiver(int port, int targetUniverse = 0)
    {
        _udpClient = new UdpClient(port);
        _targetUniverse = targetUniverse;
        _cancellationTokenSource = new CancellationTokenSource();
        _entities = new Dictionary<ushort, EntityState>();

        Console.WriteLine($"🎧 eHuBReceiver en écoute sur port {port}, univers {targetUniverse}");
    }

    /// <summary>
    /// Démarre la boucle d’écoute asynchrone
    /// </summary>
    public async Task StartAsync()
    {
        _ = Task.Run(async () =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    await ProcessMessage(result.Buffer);
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erreur eHuB : {ex.Message}");
                }
            }
        });
    }

    /// <summary>
    /// Retourne toutes les entités actuellement en mémoire
    /// </summary>
    public Dictionary<ushort, EntityState> GetCurrentEntities()
    {
        return new Dictionary<ushort, EntityState>(_entities);
    }

    public void Stop() => _cancellationTokenSource.Cancel();

    public void Dispose()
    {
        Stop();
        _udpClient?.Dispose();
        _cancellationTokenSource?.Dispose();
    }

    private async Task ProcessMessage(byte[] buffer)
    {
        if (buffer.Length < 6) return;

        // Vérifie l'entête eHuB
        if (buffer[0] != 'e' || buffer[1] != 'H' || buffer[2] != 'u' || buffer[3] != 'B')
            return;

        byte type = buffer[4];
        byte universe = buffer[5];

        if (universe != _targetUniverse)
            return;

        MessagesReceived++;

        if (type == 2)
        {
            await ProcessUpdateMessage(buffer);
        }
        else if (type == 1)
        {
            await ProcessConfigMessage(buffer);
        }
    }

    private async Task ProcessConfigMessage(byte[] buffer)
    {
        // header eHuB (4) + type (1) + universe (1) + count (2) + size (2)
        if (buffer.Length < 10) return;

        ushort rangesCount = BitConverter.ToUInt16(buffer, 6);
        ushort compressedSize = BitConverter.ToUInt16(buffer, 8);
        if (buffer.Length < 10 + compressedSize) return;

        byte[] compressed = new byte[compressedSize];
        Array.Copy(buffer, 10, compressed, 0, compressedSize);
        byte[] decompressed = Decompress(compressed);

        // Chaque bloc = 8 octets : startIndex,startId,endIndex,endId (ushort chacun)
        if (decompressed.Length < rangesCount * 8) return;

        _indexToEntityId.Clear();
        for (int i = 0; i < rangesCount; i++)
        {
            int off = i * 8;
            ushort si = BitConverter.ToUInt16(decompressed, off + 0);
            ushort sid = BitConverter.ToUInt16(decompressed, off + 2);
            ushort ei = BitConverter.ToUInt16(decompressed, off + 4);
            ushort eid = BitConverter.ToUInt16(decompressed, off + 6);

            for (ushort idx = si, id = sid; idx <= ei && id <= eid; idx++, id++)
                _indexToEntityId[idx] = id;
        }
        Console.WriteLine($"📌 {_indexToEntityId.Count} index configurés.");
    }



    private async Task ProcessUpdateMessage(byte[] buffer)
    {
        if (buffer.Length < 10) return;

        ushort entityCount = BitConverter.ToUInt16(buffer, 6);
        ushort compressedSize = BitConverter.ToUInt16(buffer, 8);

        if (buffer.Length < 10 + compressedSize) return;

        byte[] compressed = new byte[compressedSize];
        Array.Copy(buffer, 10, compressed, 0, compressedSize);

        byte[] decompressed = Decompress(compressed);

        var updated = new Dictionary<ushort, EntityState>();

        // Console.WriteLine($"\n🟢 Update reçu : {entityCount} entités");

        for (int i = 0; i < entityCount; i++)
        {
            int offset = i * 6;
            if (offset + 6 > decompressed.Length) break;

            ushort id = BitConverter.ToUInt16(decompressed, offset);
            byte r = decompressed[offset + 2];
            byte g = decompressed[offset + 3];
            byte b = decompressed[offset + 4];
            byte w = decompressed[offset + 5];

            var entity = new EntityState(id, r, g, b, w);
            _entities[id] = entity;
            updated[id] = entity;

            // Console.WriteLine($"🔸 Entity {id:0000} : R={r} G={g} B={b} W={w}");
        }

        EntitiesUpdated?.Invoke(updated);
    }

    private byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    public Dictionary<ushort, ushort> GetIndexToEntityMapping()
    {
        return new Dictionary<ushort, ushort>(_indexToEntityId);
    }

}
