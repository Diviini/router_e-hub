using System.Net;
using System.Net.Sockets;

namespace EmitterHub.eHub;

public class EHubReceiver
{
    private readonly UdpClient _udpClient;
    private readonly int _targetUniverse;
    private readonly Dictionary<ushort, EntityState> _entities = new();
    private readonly Dictionary<ushort, ushort> _indexToEntityId = new();

    public int MessagesReceived { get; private set; }
    public int ActiveEntities => _entities.Count;

    public EHubReceiver(int port, int targetUniverse = 0)
    {
        _udpClient = new UdpClient(port);
        _targetUniverse = targetUniverse;
        Console.WriteLine($"🎧 eHuBReceiver en écoute sur port {port}, univers {targetUniverse}");
    }

    public bool TryReceiveOnce()
    {
        if (_udpClient.Available > 0)
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] buffer = _udpClient.Receive(ref remoteEP);
            ProcessMessage(buffer);
            return true;
        }
        return false;
    }

    public Dictionary<ushort, EntityState> GetCurrentEntities()
    {
        lock (_entities)
        {
            return new Dictionary<ushort, EntityState>(_entities);
        }
    }

    public Dictionary<ushort, ushort> GetIndexToEntityMapping()
    {
        lock (_indexToEntityId)
        {
            return new Dictionary<ushort, ushort>(_indexToEntityId);
        }
    }

    private void ProcessMessage(byte[] buffer)
    {
        if (buffer.Length < 6) return;
        if (buffer[0] != 'e' || buffer[1] != 'H' || buffer[2] != 'u' || buffer[3] != 'B') return;

        byte type = buffer[4];
        byte universe = buffer[5];
        if (universe != _targetUniverse) return;

        MessagesReceived++;

        if (type == 2)
            ProcessUpdateMessage(buffer);
        else if (type == 1)
            ProcessConfigMessage(buffer);
    }

    private void ProcessConfigMessage(byte[] buffer)
    {
        if (buffer.Length < 14) return;
        int offset = 2;

        lock (_indexToEntityId)
        {
            while (offset + 8 <= buffer.Length)
            {
                ushort startIndex = BitConverter.ToUInt16(buffer, offset);
                ushort startId = BitConverter.ToUInt16(buffer, offset + 2);
                ushort endIndex = BitConverter.ToUInt16(buffer, offset + 4);
                ushort endId = BitConverter.ToUInt16(buffer, offset + 6);

                for (ushort index = startIndex, id = startId; index <= endIndex && id <= endId; index++, id++)
                    _indexToEntityId[index] = id;

                offset += 8;
            }
        }
    }

    private void ProcessUpdateMessage(byte[] buffer)
    {
        if (buffer.Length < 10) return;
        ushort entityCount = BitConverter.ToUInt16(buffer, 6);
        ushort compressedSize = BitConverter.ToUInt16(buffer, 8);
        if (buffer.Length < 10 + compressedSize) return;

        byte[] compressed = new byte[compressedSize];
        Array.Copy(buffer, 10, compressed, 0, compressedSize);
        byte[] decompressed = Decompress(compressed);

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

            // 🔍 Log de l'entité eHuB reçue
            // Console.WriteLine($"[eHuB] Entity {id} -> R:{r} G:{g} B:{b} W:{w}");
            lock (_entities) _entities[id] = entity;
        }
    }

    private byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}