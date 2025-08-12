using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent; // [E5] NEW
using EmitterHub.DMX;

namespace EmitterHub.ArtNet;

/// <summary>
/// Envoie les paquets ArtNet vers les contrôleurs
/// </summary>
public class ArtNetSender : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly Dictionary<string, IPEndPoint> _endpoints;
    public event Action<DmxFrame>? FrameSent;

    public int PacketsSent { get; private set; }

    // [E5] --- Stats par univers ---
        private readonly ConcurrentDictionary<int, UniverseTxStats> _stats = new();

    public ArtNetSender()
    {
        _udpClient = new UdpClient();
        _endpoints = new Dictionary<string, IPEndPoint>();
    }

    /// <summary>
    /// Envoie une trame DMX via ArtNet
    /// </summary>
    public async Task SendDmxFrameAsync(DmxFrame frame)
    {
        var packet = new ArtNetPacket(frame);

        if (!_endpoints.TryGetValue(frame.TargetIP, out var endpoint))
        {
            endpoint = new IPEndPoint(IPAddress.Parse(frame.TargetIP), ArtNetPacket.ARTNET_PORT);
            _endpoints[frame.TargetIP] = endpoint;
        }

        await _udpClient.SendAsync(packet.PacketData, packet.PacketSize, endpoint);
        PacketsSent++;

         // [E5] MAJ stats
            var active = frame.Channels.Count(b => b > 0);
            var now = DateTime.UtcNow;

            var st = _stats.GetOrAdd(frame.Universe, _ => new UniverseTxStats(frame.Universe));
            st.TargetIP = frame.TargetIP;
            st.TotalPackets++;
            st.TotalBytes += packet.PacketSize;
            st.LastSentUtc = now;
            st.LastActiveChannels = active;
            st.TicksThisSecond++;

        FrameSent?.Invoke(frame);
    }
    // [E5] appeler depuis le VM toutes les 250 ms
    public List<UniverseTxSnapshot> GetStatsSnapshot(bool activeOnly, out int totalPps, out int totalBps)
    {
        var now = DateTime.UtcNow;
        totalPps = 0;
        totalBps = 0;

        var list = new List<UniverseTxSnapshot>(_stats.Count);
        foreach (var kv in _stats)
        {
            var s = kv.Value;
            var snap = s.ToSnapshotAndMaybeFlip(now);

            if (!activeOnly || snap.PacketRatePerSec > 0 || snap.LastActiveChannels > 0)
                list.Add(snap);

            totalPps += snap.PacketRatePerSec;
            totalBps += snap.ByteRatePerSec;
        }
        // tri par univers
        list.Sort((a, b) => a.Universe.CompareTo(b.Universe));
        return list;
    }

    public void Dispose()
    {
        _udpClient?.Close();
        _udpClient?.Dispose();
        Console.WriteLine($"ArtNet Sender arrêté. Paquets envoyés: {PacketsSent}");
    }
    // [E5] classes stats
    public sealed class UniverseTxStats
    {
        private int _ppsAcc;
        private int _bpsAcc;
        private long _bytesThisSecond;
        private DateTime _lastSecondFlip = DateTime.UtcNow;

        public int Universe { get; }
        public string TargetIP { get; set; } = string.Empty;

        public int TotalPackets { get; set; }
        public long TotalBytes { get; set; }
        public int LastActiveChannels { get; set; }
        public DateTime LastSentUtc { get; set; }
        public int TicksThisSecond { get; set; } // accumulateur interne

        public UniverseTxStats(int universe) => Universe = universe;

        // appelé ~4 fois/s; on convertit en pps/bps à la seconde
        public UniverseTxSnapshot ToSnapshotAndMaybeFlip(DateTime nowUtc)
        {
            if ((nowUtc - _lastSecondFlip).TotalSeconds >= 1.0)
            {
                _ppsAcc = TicksThisSecond;
                _bpsAcc = (int)_bytesThisSecond;
                TicksThisSecond = 0;
                _bytesThisSecond = 0;
                _lastSecondFlip = nowUtc;
            }

            return new UniverseTxSnapshot
            {
                Universe = Universe,
                TargetIP = TargetIP,
                PacketRatePerSec = _ppsAcc,
                ByteRatePerSec = _bpsAcc,
                LastActiveChannels = LastActiveChannels,
                LastSentLocal = LastSentUtc.ToLocalTime()
            };
        }

        public void AddBytesThisTick(int bytes) => _bytesThisSecond += bytes;
    }

    public sealed class UniverseTxSnapshot
    {
        public int Universe { get; set; }
        public string TargetIP { get; set; } = string.Empty;
        public int PacketRatePerSec { get; set; }     // paquets/s
        public int ByteRatePerSec { get; set; }       // octets/s
        public int LastActiveChannels { get; set; }   // non-zéro dans la dernière trame
        public DateTime LastSentLocal { get; set; }   // debug
    }

}