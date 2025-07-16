using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using EmitterHub.ArtNet;
using EmitterHub.DMX;

public class ArtNetSender : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly ConcurrentDictionary<string, IPEndPoint> _endpoints;

    public int PacketsSent { get; private set; }

    // Nouvelle option : forcer l'envoi même si la trame est vide
    public bool SendAllFrames { get; set; } = false;

    public ArtNetSender()
    {
        _udpClient = new UdpClient();
        _endpoints = new ConcurrentDictionary<string, IPEndPoint>();
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
    }

    public void Dispose()
    {
        _udpClient?.Close();
        _udpClient?.Dispose();
        Console.WriteLine($"ArtNet Sender arrêté. Paquets envoyés: {PacketsSent}");
    }
}
