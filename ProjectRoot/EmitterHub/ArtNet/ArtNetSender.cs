using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, IPEndPoint> _endpoints;

    public int PacketsSent { get; private set; }
    public int MaxFrameRate { get; set; } = 40; // FPS maximum

    public ArtNetSender()
    {
        _udpClient = new UdpClient();
        _endpoints = new ConcurrentDictionary<string, IPEndPoint>();

        Console.WriteLine($"ArtNet Sender initialisé (max {MaxFrameRate} FPS)");
    }

    /// <summary>
    /// Envoie une trame DMX via ArtNet
    /// </summary>
    public async Task SendDmxFrameAsync(DmxFrame frame)
    {
        if (frame == null || !frame.IsModified) return;

        var packet = new ArtNetPacket(frame);

        var endpoint = _endpoints.GetOrAdd(frame.TargetIP, 
            ip => new IPEndPoint(IPAddress.Parse(ip), ArtNetPacket.ARTNET_PORT));

        await _udpClient.SendAsync(packet.PacketData, packet.PacketSize, endpoint);
        PacketsSent++;
        frame.MarkAsSent(); // Reset the IsModified flag after sending
    }

    public void Dispose()
    {
        _udpClient?.Close();
        _udpClient?.Dispose();
        Console.WriteLine($"ArtNet Sender arrêté. Paquets envoyés: {PacketsSent}");
    }
}