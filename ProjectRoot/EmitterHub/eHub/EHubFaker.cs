using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EmitterHub.eHub
{
    public class EHubFaker : IDisposable
    {
        private readonly UdpClient _udp = new();
        private readonly IPEndPoint _target;
        private CancellationTokenSource? _cts;

        public int EhubUniverse { get; set; } = 1; // aligne avec ton receiver
        public byte SolidR { get; set; } = 255;
        public byte SolidG { get; set; } = 255;
        public byte SolidB { get; set; } = 255;

        // Les plages complètes à animer (venant du router)
        private List<(ushort Start, ushort End)> _ranges = new();

        // Index→ID (pour CONFIG); calculé à partir de _ranges
        private List<(ushort StartIndex, ushort StartId, ushort EndIndex, ushort EndId)> _indexToId = new();

        // chunking: nombre maximal d’entités par paquet update (avant compression)
        private const int MaxEntitiesPerUpdatePacket = 1500; // safe

        public EHubFaker(int port = 8765)
        {
            _target = new IPEndPoint(IPAddress.Loopback, port);
        }

        public void SetRangesFromRouter(IReadOnlyList<EmitterHub.DMX.EntityRange> ranges)
        {
            _ranges = ranges
                .Select(r => ((ushort)r.Start, (ushort)r.End))
                .OrderBy(t => t.Item1)
                .ToList();

            // Construire la table Index→ID
            _indexToId.Clear();
            int runningIndex = 0; // “sextuor index” logique (0..N-1)
            foreach (var (start, end) in _ranges)
            {
                int len = end - start + 1;
                ushort startIndex = (ushort)runningIndex;
                ushort endIndex = (ushort)(runningIndex + len - 1);
                _indexToId.Add((startIndex, start, endIndex, end));
                runningIndex += len;
            }
        }

        public void Start()
        {
            if (_cts != null && !_cts.IsCancellationRequested) return;
            _cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try
                {
                    // CONFIG d’abord
                    await SendConfigAsync();

                    // boucle update ~40 Hz
                    while (!_cts.IsCancellationRequested)
                    {
                        await SendSolidUpdateAsync();
                        await Task.Delay(25, _cts.Token);
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EHubFaker] Erreur: {ex.Message}");
                }
            });
        }

        public void Stop() => _cts.Cancel();

        public void Dispose()
        {
            Stop();
            _udp.Dispose();
        }

        private async Task SendConfigAsync()
        {
            // payload décompressé = concat des plages (8 octets par plage)
            using var raw = new MemoryStream();
            using (var bw = new BinaryWriter(raw, Encoding.UTF8, true))
            {
                foreach (var (si, sid, ei, eid) in _indexToId)
                {
                    bw.Write(si);
                    bw.Write(sid);
                    bw.Write(ei);
                    bw.Write(eid);
                }
            }
            var compressed = Compress(raw.ToArray());

            using var packet = new MemoryStream();
            using (var bw = new BinaryWriter(packet, Encoding.UTF8, true))
            {
                bw.Write(Encoding.ASCII.GetBytes("eHuB"));
                bw.Write((byte)1);              // config
                bw.Write((byte)EhubUniverse);   // univers eHuB
                bw.Write((ushort)_indexToId.Count); // nb de plages
                bw.Write((ushort)compressed.Length);
                bw.Write(compressed);
            }

            await _udp.SendAsync(packet.ToArray(), (int)packet.Length, _target);
        }

        private async Task SendSolidUpdateAsync()
        {
            // On parcourt toutes les entités, mais on segmente en paquets
            // pour respecter MaxEntitiesPerUpdatePacket
            foreach (var chunk in EnumerateChunks())
            {
                using var raw = new MemoryStream();
                using (var bw = new BinaryWriter(raw, Encoding.UTF8, true))
                {
                    foreach (ushort id in chunk)
                    {
                        bw.Write(id);
                        bw.Write(SolidR);
                        bw.Write(SolidG);
                        bw.Write(SolidB);
                        bw.Write((byte)0); // W
                    }
                }
                var compressed = Compress(raw.ToArray());

                using var packet = new MemoryStream();
                using (var bw = new BinaryWriter(packet, Encoding.UTF8, true))
                {
                    bw.Write(Encoding.ASCII.GetBytes("eHuB"));
                    bw.Write((byte)2);            // update
                    bw.Write((byte)EhubUniverse); // univers eHuB
                    bw.Write((ushort)chunk.Count); // entityCount
                    bw.Write((ushort)compressed.Length);
                    bw.Write(compressed);
                }

                await _udp.SendAsync(packet.ToArray(), (int)packet.Length, _target);
            }
        }

        private IEnumerable<List<ushort>> EnumerateChunks()
        {
            var buffer = new List<ushort>(MaxEntitiesPerUpdatePacket);
            foreach (var (start, end) in _ranges)
            {
                for (ushort id = start; id <= end; id++)
                {
                    buffer.Add(id);
                    if (buffer.Count >= MaxEntitiesPerUpdatePacket)
                    {
                        yield return buffer;
                        buffer = new List<ushort>(MaxEntitiesPerUpdatePacket);
                    }
                }
            }
            if (buffer.Count > 0)
                yield return buffer;
        }

        private static byte[] Compress(byte[] data)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Fastest))
            {
                gz.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }
    }
}
