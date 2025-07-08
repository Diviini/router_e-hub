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
    public async Task SendDmxFrameAsync(DmxFrame frame)
    {
        if (frame == null || !frame.IsModified) return;

        // If the frame is marked as modified, we send it.
        // This covers cases where it becomes active, changes data, or becomes inactive.
        // DmxFrame.HasData() is still useful if one wants to know if it *currently* has active channels,
        // but IsModified is the trigger for sending.

        var packet = new ArtNetPacket(frame);

        if (!_endpoints.TryGetValue(frame.TargetIP, out var endpoint))
        {
            endpoint = new IPEndPoint(IPAddress.Parse(frame.TargetIP), ArtNetPacket.ARTNET_PORT);
            _endpoints[frame.TargetIP] = endpoint;
        }

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