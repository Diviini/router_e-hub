using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace EmitterHub.ArtNet
{
    public class ArtNetListener : IDisposable
    {
        private readonly UdpClient _udp;
        private readonly CancellationTokenSource _cts = new();

        public event Action<ArtnetFrameRow>? FrameReceived;

        public ArtNetListener(int port = 6454)
        {
            // IMPORTANT : écoute sur Any pour recevoir aussi 127.0.0.1
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            // Optionnel : pour réécouter sans attendre TIME_WAIT
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public void Start()
        {
            _ = Task.Run(Loop);
        }

        private async Task Loop()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var res = await _udp.ReceiveAsync(_cts.Token);
                    var info = ArtNetPacket.ParsePacket(res.Buffer);
                    if (info != null)
                    {
                        var row = new ArtnetFrameRow
                        {
                            Universe        = info.Universe,
                            Length          = info.DataLength,
                            ActiveChannels  = info.ActiveChannels,
                            SourceIP        = res.RemoteEndPoint.Address.ToString(),
                            Timestamp       = DateTime.Now
                        };
                        FrameReceived?.Invoke(row);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[ArtNetListener] erreur: {ex.Message}");
            }
        }

        public void Stop() => _cts.Cancel();

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            _udp?.Close();
            _udp?.Dispose();
            _cts.Dispose();
        }
    }

    public class ArtnetFrameRow
    {
        public int Universe { get; set; }
        public int Length { get; set; }
        public int ActiveChannels { get; set; }
        public string SourceIP { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");
    }
}
