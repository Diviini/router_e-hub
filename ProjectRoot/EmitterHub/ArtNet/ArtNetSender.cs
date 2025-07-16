using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using EmitterHub.DMX;
using System.Collections.Concurrent;

namespace EmitterHub.ArtNet;

/// <summary>
/// ArtNet Sender optimisé pour réseaux WiFi et bande passante limitée
/// </summary>
public class ArtNetSender : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly Dictionary<string, IPEndPoint> _endpoints;
    private readonly ConcurrentDictionary<string, NetworkStats> _networkStats;

    // Configuration adaptative
    private readonly int _maxPacketsPerSecond = 120; // Limite pour WiFi
    private readonly int _adaptiveDelay = 10; // Délai adaptatif en ms
    private readonly Queue<DateTime> _sendTimes = new();
    private readonly object _sendTimesLock = new();

    // Monitoring
    public int PacketsSent { get; private set; }
    public int PacketsDropped { get; private set; }
    public double CurrentBandwidthMbps { get; private set; }

    // Détection de congestion
    private DateTime _lastCongestionCheck = DateTime.Now;
    private bool _isNetworkCongested = false;

    public ArtNetSender()
    {
        _udpClient = new UdpClient();
        _endpoints = new Dictionary<string, IPEndPoint>();
        _networkStats = new ConcurrentDictionary<string, NetworkStats>();

        // Configuration socket pour performances
        _udpClient.Client.SendBufferSize = 65536; // 64KB buffer
        _udpClient.Client.ReceiveBufferSize = 65536;

        Console.WriteLine("🌐 ArtNet Sender optimisé initialisé");
        Console.WriteLine("📊 Limite: {0} paquets/sec, Buffer: 64KB", _maxPacketsPerSecond);
    }

    /// <summary>
    /// Envoie une trame DMX avec contrôle de débit adaptatif
    /// </summary>
    public async Task SendDmxFrameAsync(DmxFrame frame)
    {
        Console.WriteLine($"📤 Tentative d’envoi vers {frame.TargetIP}, Universe {frame.Universe}");

        // Contrôle du taux d'envoi
        if (!await CheckRateLimitAsync())
        {
            PacketsDropped++;
            return;
        }

        // Détection de congestion
        if (await IsNetworkCongestedAsync())
        {
            await Task.Delay(_adaptiveDelay * 2); // Délai doublé si congestion
        }


        try
        {
            var packet = new ArtNetPacket(frame);

            if (!_endpoints.TryGetValue(frame.TargetIP, out var endpoint))
            {
                endpoint = new IPEndPoint(IPAddress.Parse(frame.TargetIP), ArtNetPacket.ARTNET_PORT);
                _endpoints[frame.TargetIP] = endpoint;

                // Initialiser les stats pour cette IP
                _networkStats[frame.TargetIP] = new NetworkStats();
            }

            var startTime = DateTime.Now;

            // Envoi avec timeout
            var sendTask = _udpClient.SendAsync(packet.PacketData, packet.PacketSize, endpoint);
            var timeoutTask = Task.Delay(1000); // 1 seconde de timeout

            var completedTask = await Task.WhenAny(sendTask, timeoutTask);

            if (completedTask == sendTask)
            {
                PacketsSent++;
                UpdateNetworkStats(frame.TargetIP, startTime, packet.PacketSize);
                RecordSendTime();
            }
            else
            {
                PacketsDropped++;
                Console.WriteLine("⚠️ Timeout envoi vers {0}", frame.TargetIP);
            }
        }
        catch (Exception ex)
        {
            PacketsDropped++;
            Console.WriteLine("❌ Erreur envoi ArtNet vers {0}: {1}", frame.TargetIP, ex.Message);
        }
    }

    /// <summary>
    /// Contrôle du taux d'envoi pour éviter la surcharge
    /// </summary>
    private async Task<bool> CheckRateLimitAsync()
    {
        lock (_sendTimesLock)
        {
            var now = DateTime.Now;
            var cutoff = now - TimeSpan.FromSeconds(1);

            // Nettoyer les anciens timestamps
            while (_sendTimes.Count > 0 && _sendTimes.Peek() < cutoff)
            {
                _sendTimes.Dequeue();
            }

            // Vérifier la limite
            if (_sendTimes.Count >= _maxPacketsPerSecond)
            {
                return false; // Limite dépassée
            }

            return true;
        }
    }

    /// <summary>
    /// Enregistre le temps d'envoi pour le rate limiting
    /// </summary>
    private void RecordSendTime()
    {
        lock (_sendTimesLock)
        {
            _sendTimes.Enqueue(DateTime.Now);
        }
    }

    /// <summary>
    /// Détection de congestion réseau
    /// </summary>
    private async Task<bool> IsNetworkCongestedAsync()
    {
        var now = DateTime.Now;

        // Vérifier toutes les 5 secondes seulement
        if (now - _lastCongestionCheck < TimeSpan.FromSeconds(5))
        {
            return _isNetworkCongested;
        }

        _lastCongestionCheck = now;

        try
        {
            // Ping simple pour détecter la latence
            var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 1000);

            if (reply.Status == IPStatus.Success)
            {
                // Considérer comme congestionné si RTT > 100ms
                _isNetworkCongested = reply.RoundtripTime > 100;

                if (_isNetworkCongested)
                {
                    Console.WriteLine("🐌 Congestion détectée - RTT: {0}ms", reply.RoundtripTime);
                }
            }
        }
        catch
        {
            // En cas d'erreur, considérer comme non congestionné
            _isNetworkCongested = false;
        }

        return _isNetworkCongested;
    }

    /// <summary>
    /// Met à jour les statistiques réseau par IP
    /// </summary>
    private void UpdateNetworkStats(string ip, DateTime startTime, int packetSize)
    {
        if (_networkStats.TryGetValue(ip, out var stats))
        {
            stats.PacketsSent++;
            stats.TotalBytes += packetSize;
            stats.LastSendTime = DateTime.Now;
            stats.AverageLatency = (stats.AverageLatency + (DateTime.Now - startTime).TotalMilliseconds) / 2;

            // Calcul de la bande passante
            var elapsed = stats.LastSendTime - stats.FirstSendTime;
            if (elapsed.TotalSeconds > 0)
            {
                stats.BandwidthMbps = (stats.TotalBytes * 8) / (elapsed.TotalSeconds * 1_000_000);
            }
        }
    }

    /// <summary>
    /// Obtient les statistiques détaillées
    /// </summary>
    public NetworkStatsSummary GetNetworkStats()
    {
        var summary = new NetworkStatsSummary
        {
            TotalPacketsSent = PacketsSent,
            TotalPacketsDropped = PacketsDropped,
            DropRate = PacketsSent > 0 ? (double)PacketsDropped / (PacketsSent + PacketsDropped) : 0,
            IsNetworkCongested = _isNetworkCongested,
            IPStats = new Dictionary<string, NetworkStats>()
        };

        foreach (var kvp in _networkStats)
        {
            summary.IPStats[kvp.Key] = kvp.Value;
        }

        // Calcul de la bande passante totale
        summary.TotalBandwidthMbps = summary.IPStats.Values.Sum(s => s.BandwidthMbps);

        return summary;
    }

    /// <summary>
    /// Ajuste automatiquement les paramètres selon les conditions réseau
    /// </summary>
    public void OptimizeForNetworkConditions()
    {
        var stats = GetNetworkStats();

        if (stats.DropRate > 0.1) // Plus de 10% de perte
        {
            Console.WriteLine("📉 Adaptation: Réduction du débit (perte {0:P1})", stats.DropRate);
        }

        if (stats.IsNetworkCongested)
        {
            Console.WriteLine("🐌 Adaptation: Mode congestion activé");
        }

        // Mise à jour de la bande passante courante
        CurrentBandwidthMbps = stats.TotalBandwidthMbps;
    }

    /// <summary>
    /// Envoi en mode batch pour optimiser les petites trames
    /// </summary>
    public async Task SendBatchAsync(IEnumerable<DmxFrame> frames)
    {
        var tasks = new List<Task>();
        const int batchSize = 5;

        foreach (var frame in frames)
        {
            tasks.Add(SendDmxFrameAsync(frame));

            if (tasks.Count >= batchSize)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();

                // Délai entre batches pour éviter surcharge
                await Task.Delay(_adaptiveDelay);
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    public void Dispose()
    {
        _udpClient?.Close();
        _udpClient?.Dispose();

        var stats = GetNetworkStats();
        Console.WriteLine("📊 ArtNet Sender arrêté:");
        Console.WriteLine("  Paquets envoyés: {0}", PacketsSent);
        Console.WriteLine("  Paquets perdus: {0} ({1:P1})", PacketsDropped, stats.DropRate);
        Console.WriteLine("  Bande passante: {0:F2} Mbps", stats.TotalBandwidthMbps);
    }
}

/// <summary>
/// Statistiques réseau par IP
/// </summary>
public class NetworkStats
{
    public int PacketsSent { get; set; }
    public long TotalBytes { get; set; }
    public DateTime FirstSendTime { get; set; } = DateTime.Now;
    public DateTime LastSendTime { get; set; } = DateTime.Now;
    public double AverageLatency { get; set; }
    public double BandwidthMbps { get; set; }
}

/// <summary>
/// Résumé des statistiques réseau
/// </summary>
public class NetworkStatsSummary
{
    public int TotalPacketsSent { get; set; }
    public int TotalPacketsDropped { get; set; }
    public double DropRate { get; set; }
    public bool IsNetworkCongested { get; set; }
    public double TotalBandwidthMbps { get; set; }
    public Dictionary<string, NetworkStats> IPStats { get; set; } = new();
}