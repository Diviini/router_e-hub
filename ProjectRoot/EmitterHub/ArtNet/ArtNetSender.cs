using System.Net;
using System.Net.Sockets;
using EmitterHub.DMX;

namespace EmitterHub.ArtNet;

/// <summary>
/// Envoie les paquets ArtNet vers les contrôleurs
/// </summary>
public class ArtNetSender : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly Dictionary<string, IPEndPoint> _endpoints;
    private readonly object _lockObject = new object();

    // Contrôle de débit
    private DateTime _lastSendTime = DateTime.MinValue;
    private readonly TimeSpan _minSendInterval = TimeSpan.FromMilliseconds(25); // 40 FPS max

    public int PacketsSent { get; private set; }
    public int MaxFrameRate { get; set; } = 40; // FPS maximum

    public ArtNetSender()
    {
        _udpClient = new UdpClient();
        _endpoints = new Dictionary<string, IPEndPoint>();

        Console.WriteLine($"ArtNet Sender initialisé (max {MaxFrameRate} FPS)");
    }

    /// <summary>
    /// Envoie une trame DMX via ArtNet
    /// </summary>
    public async Task SendDmxFrameAsync(DmxFrame frame) { }
}