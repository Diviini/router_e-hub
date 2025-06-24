using System.IO.Compression;
using System.Net;
using System.Net.Sockets;

namespace EmitterHub.eHub;

/// <summary>
/// Récepteur pour les messages eHuB (protocole personnalisé Unity/Tan)
/// </summary>
public class EHubReceiver : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly int _targetUniverse;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Dictionary<ushort, EntityState> _entities;
    private readonly Dictionary<ushort, (ushort startEntity, ushort endEntity)> _ranges;

    public event Action<Dictionary<ushort, EntityState>>? EntitiesUpdated;
    public event Action<Dictionary<ushort, (ushort startEntity, ushort endEntity)>>? ConfigUpdated;

    public int MessagesReceived { get; private set; }
    public int ActiveEntities => _entities.Count;

    public EHubReceiver(int port, int targetUniverse = 0)
    {
        _targetUniverse = targetUniverse;
        _cancellationTokenSource = new CancellationTokenSource();
        _entities = new Dictionary<ushort, EntityState>();
        _ranges = new Dictionary<ushort, (ushort, ushort)>();

        _udpClient = new UdpClient(port);
        Console.WriteLine($"eHuB Receiver listening on port {port}, universe {targetUniverse}");
    }

    /// <summary>
    /// Démarre l'écoute des messages eHuB
    /// </summary>
    public async Task StartAsync()
    {
        await Task.Run(async () =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    await ProcessMessage(result.Buffer);
                }
                catch (ObjectDisposedException)
                {
                    // Le client UDP a été fermé
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur réception eHuB: {ex.Message}");
                }
            }
        });
    }

    /// <summary>
    /// Traite un message eHuB reçu
    /// </summary>
    private async Task ProcessMessage(byte[] buffer)
    {
        if (buffer.Length < 6) return;

        // Vérification signature "eHuB"
        if (buffer[0] != 'e' || buffer[1] != 'H' || buffer[2] != 'u' || buffer[3] != 'B')
            return;

        byte messageType = buffer[4];
        byte universe = buffer[5];

        // Filtrer par univers cible
        if (universe != _targetUniverse) return;

        MessagesReceived++;

        switch (messageType)
        {
            case 1: // Config message
                await ProcessConfigMessage(buffer);
                break;
            case 2: // Update message  
                await ProcessUpdateMessage(buffer);
                break;
        }
    }

    /// <summary>
    /// Traite un message de configuration
    /// </summary>
    private async Task ProcessConfigMessage(byte[] buffer)
    {
        if (buffer.Length < 10) return;

        ushort rangeCount = BitConverter.ToUInt16(buffer, 6);
        ushort compressedSize = BitConverter.ToUInt16(buffer, 8);

        if (buffer.Length < 10 + compressedSize) return;

        // Décompression
        byte[] compressed = new byte[compressedSize];
        Array.Copy(buffer, 10, compressed, 0, compressedSize);

        byte[] decompressed = Decompress(compressed);

        // Lecture des ranges (8 octets par range)
        _ranges.Clear();
        for (int i = 0; i < rangeCount && i * 8 < decompressed.Length; i++)
        {
            int offset = i * 8;
            ushort payloadStart = BitConverter.ToUInt16(decompressed, offset);
            ushort entityStart = BitConverter.ToUInt16(decompressed, offset + 2);
            ushort payloadEnd = BitConverter.ToUInt16(decompressed, offset + 4);
            ushort entityEnd = BitConverter.ToUInt16(decompressed, offset + 6);

            _ranges[payloadStart] = (entityStart, entityEnd);
        }

        Console.WriteLine($"Config reçue: {rangeCount} ranges");
        ConfigUpdated?.Invoke(_ranges);
    }

    /// <summary>
    /// Traite un message de mise à jour des entités
    /// </summary>
    private async Task ProcessUpdateMessage(byte[] buffer)
    {
        if (buffer.Length < 12) return;

        ushort entityCount = BitConverter.ToUInt16(buffer, 6);
        ushort compressedSize = BitConverter.ToUInt16(buffer, 8);

        if (buffer.Length < 10 + compressedSize) return;

        // Décompression
        byte[] compressed = new byte[compressedSize];
        Array.Copy(buffer, 10, compressed, 0, compressedSize);

        byte[] decompressed = Decompress(compressed);

        // Lecture des entités (6 octets par entité: ID + RGBW)
        var updatedEntities = new Dictionary<ushort, EntityState>();

        for (int i = 0; i < entityCount && i * 6 < decompressed.Length; i++)
        {
            int offset = i * 6;
            ushort id = BitConverter.ToUInt16(decompressed, offset);
            byte r = decompressed[offset + 2];
            byte g = decompressed[offset + 3];
            byte b = decompressed[offset + 4];
            byte w = decompressed[offset + 5];

            var entity = new EntityState(id, r, g, b, w);
            _entities[id] = entity;
            updatedEntities[id] = entity;
        }

        EntitiesUpdated?.Invoke(updatedEntities);
    }

    /// <summary>
    /// Décompresse les données GZip
    /// </summary>
    private byte[] Decompress(byte[] compressed)
    {
        using var compressedStream = new MemoryStream(compressed);
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var resultStream = new MemoryStream();

        gzipStream.CopyTo(resultStream);
        return resultStream.ToArray();
    }

    /// <summary>
    /// Obtient l'état actuel de toutes les entités
    /// </summary>
    public Dictionary<ushort, EntityState> GetCurrentEntities()
    {
        return new Dictionary<ushort, EntityState>(_entities);
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
    }

    public void Dispose()
    {
        Stop();
        _udpClient?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}