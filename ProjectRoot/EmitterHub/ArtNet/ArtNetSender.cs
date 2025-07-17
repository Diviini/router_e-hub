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
    public event Action<DmxFrame>? FrameSent;

    public int PacketsSent { get; private set; }

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

        FrameSent?.Invoke(frame);
    }

    public void Dispose()
    {
        _udpClient?.Close();
        _udpClient?.Dispose();
        Console.WriteLine($"ArtNet Sender arrêté. Paquets envoyés: {PacketsSent}");
    }
}