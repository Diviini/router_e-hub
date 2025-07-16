using System.Buffers;
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
    public Task StartAsync()
    {
        _ = Task.Run(async () =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    // On ne bloque pas la boucle de réception, on traite le message en arrière plan
                    _ = Task.Run(() => ProcessMessage(result.Buffer), _cancellationTokenSource.Token);
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erreur eHuB : {ex.Message}");
                }
            }
        });
        return Task.CompletedTask;
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

    private void ProcessMessage(byte[] buffer)
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
            ProcessUpdateMessage(buffer);
        }
        else if (type == 1)
        {
            ProcessConfigMessage(buffer);
        }
    }

    private void ProcessConfigMessage(byte[] buffer)
    {
        // Minimum = header eHuB (6) + 1 groupe (8) = 14 octets
        if (buffer.Length < 14) return;

        int offset = 6; // On saute le header eHuB

        while (offset + 8 <= buffer.Length)
        {
            ushort startIndex = BitConverter.ToUInt16(buffer, offset);
            ushort startId = BitConverter.ToUInt16(buffer, offset + 2);
            ushort endIndex = BitConverter.ToUInt16(buffer, offset + 4);
            ushort endId = BitConverter.ToUInt16(buffer, offset + 6);

            for (ushort index = startIndex, id = startId;
                 index <= endIndex && id <= endId;
                 index++, id++)
            {
                _indexToEntityId[index] = id;
            }

            offset += 8;
        }

        Console.WriteLine($"📌 {_indexToEntityId.Count} index configurés.");
    }

    private void ProcessUpdateMessage(byte[] buffer)
    {
        Console.WriteLine($"🟢 Update reçu ? Buffer size: {buffer.Length}, Expected min: 10");

        if (buffer.Length < 10) return;

        ushort entityCount = BitConverter.ToUInt16(buffer, 6);
        ushort compressedSize = BitConverter.ToUInt16(buffer, 8);

        if (buffer.Length < 10 + compressedSize) return;

        var compressedSpan = new ReadOnlySpan<byte>(buffer, 10, compressedSize);
        byte[]? decompressedBuffer = null;
        try
        {
            decompressedBuffer = Decompress(compressedSpan);
            var updated = new Dictionary<ushort, EntityState>();

            for (int i = 0; i < entityCount; i++)
            {
                int offset = i * 6;
                if (offset + 6 > decompressedBuffer.Length) break;

                ushort id = BitConverter.ToUInt16(decompressedBuffer, offset);
                byte r = decompressedBuffer[offset + 2];
                byte g = decompressedBuffer[offset + 3];
                byte b = decompressedBuffer[offset + 4];
                byte w = decompressedBuffer[offset + 5];

                var entity = new EntityState(id, r, g, b, w);
                _entities[id] = entity;
                updated[id] = entity;
            }
            Console.WriteLine($"🔁 {updated.Count} entités mises à jour");

            EntitiesUpdated?.Invoke(updated);
        }
        finally
        {
            if (decompressedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(decompressedBuffer);
            }
        }
    }

    private byte[] Decompress(ReadOnlySpan<byte> compressed)
    {
        // On loue un buffer pour la décompression pour éviter les allocations
        byte[] decompressed = ArrayPool<byte>.Shared.Rent(compressed.Length * 5); // Estimation
        int bytesWritten;
        using (var input = new MemoryStream(compressed.ToArray()))
        using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
        {
            bytesWritten = zlib.Read(decompressed, 0, decompressed.Length);
        }
        return decompressed;
    }

    public Dictionary<ushort, ushort> GetIndexToEntityMapping()
    {
        return new Dictionary<ushort, ushort>(_indexToEntityId);
    }
}
