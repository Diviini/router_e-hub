using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using EmitterHub.DMX;

namespace EmitterHub.ArtNet
{
    /// <summary>
    /// ArtNetSender
    /// -------------
    /// - Construit et envoie des paquets Art-Net (ArtDMX) via UDP.
    /// - Expose un événement FrameSent pour le monitoring local (UI).
    /// - Option "EchoToLocal" : recopie chaque paquet vers 127.0.0.1:6454
    ///   pour permettre à un autre process / moniteur ArtNet local d'écouter.
    /// - Maintient des statistiques par univers (pps / bps / derniers canaux actifs).
    /// 
    /// NOTE : Implémentation thread-safe pour les stats via ConcurrentDictionary.
    /// </summary>
    public class ArtNetSender : IDisposable
    {
        // ==========================
        //      CHAMPS PRIVÉS
        // ==========================

        private readonly UdpClient _udpClient;                         // Socket UDP d'envoi
        private readonly Dictionary<string, IPEndPoint> _endpoints;    // Cache IP -> endpoint
        private readonly ConcurrentDictionary<int, UniverseTxStats> _stats = new(); // Stats/univers

        // ==========================
        //      ÉVÉNEMENTS / PROPS
        // ==========================

        /// <summary>
        /// Notifié après l'envoi d'une trame DMX (utile côté UI pour moniteur temps réel).
        /// </summary>
        public event Action<DmxFrame>? FrameSent;

        /// <summary>
        /// Nombre total de paquets ArtNet envoyés depuis le démarrage.
        /// </summary>
        public int PacketsSent { get; private set; }

        /// <summary>
        /// Si true, envoie une copie du paquet à 127.0.0.1:6454 (boucle locale).
        /// Utile pour un moniteur ArtNet sur la même machine (ex: notre listener E9).
        /// </summary>
        public bool EchoToLocal { get; set; } = true;

        // ==========================
        //         CTOR / DISPOSE
        // ==========================

        public ArtNetSender()
        {
            _udpClient = new UdpClient();
            _endpoints = new Dictionary<string, IPEndPoint>();
        }

        public void Dispose()
        {
            _udpClient?.Close();
            _udpClient?.Dispose();
            Console.WriteLine($"ArtNet Sender arrêté. Paquets envoyés: {PacketsSent}");
        }

        // ==========================
        //     ENVOI D'UNE TRAME
        // ==========================

        /// <summary>
        /// Construit un paquet Art-Net à partir d'un DmxFrame et l'envoie à l'IP cible.
        /// Met à jour les statistiques par univers et déclenche FrameSent.
        /// </summary>
        public async Task SendDmxFrameAsync(DmxFrame frame)
        {
            // 1) Construire le paquet ArtDMX
            var packet = new ArtNetPacket(frame);

            // 2) Résoudre (ou mémoriser) l'endpoint de destination
            if (!_endpoints.TryGetValue(frame.TargetIP, out var endpoint))
            {
                endpoint = new IPEndPoint(IPAddress.Parse(frame.TargetIP), ArtNetPacket.ARTNET_PORT);
                _endpoints[frame.TargetIP] = endpoint;
            }

            // 3) Envoyer au contrôleur
            await _udpClient.SendAsync(packet.PacketData, packet.PacketSize, endpoint);
            PacketsSent++;

            // 4) Éventuellement, miroir local pour moniteur ArtNet
            if (EchoToLocal)
            {
                var loopback = new IPEndPoint(IPAddress.Loopback, ArtNetPacket.ARTNET_PORT);
                await _udpClient.SendAsync(packet.PacketData, packet.PacketSize, loopback);
            }

            // 5) Mettre à jour les stats par univers
            var nowUtc = DateTime.UtcNow;
            var active = frame.Channels.Count(b => b > 0);

            var stat = _stats.GetOrAdd(frame.Universe, u => new UniverseTxStats(u));
            stat.TargetIP = frame.TargetIP;
            stat.TotalPackets++;
            stat.TotalBytes += packet.PacketSize;
            stat.LastSentUtc = nowUtc;
            stat.LastActiveChannels = active;
            stat.TicksThisSecond++;                 // incrément du compteur de paquets/s
            stat.AddBytesThisTick(packet.PacketSize); // incrément du compteur d’octets/s

            // 6) Notifier l'UI
            FrameSent?.Invoke(frame);
        }

        // ==========================
        //        STATISTIQUES
        // ==========================

        /// <summary>
        /// Retourne une photographie des stats par univers.
        /// - activeOnly : filtre les univers inactifs (pps=0 et canaux=0)
        /// - totalPps / totalBps : totaux agrégés
        /// </summary>
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

            // Tri par numéro d'univers pour un affichage stable
            list.Sort((a, b) => a.Universe.CompareTo(b.Universe));
            return list;
        }

        // ==========================
        //        TYPES STATS
        // ==========================

        /// <summary>
        /// État interne (mutable) des compteurs pour un univers.
        /// </summary>
        public sealed class UniverseTxStats
        {
            // Accumulateurs internes pour le "flip" par seconde
            private int _ppsAcc;
            private int _bpsAcc;
            private long _bytesThisSecond;
            private DateTime _lastSecondFlip = DateTime.UtcNow;

            public int Universe { get; }
            public string TargetIP { get; set; } = string.Empty;

            // Compteurs cumulés (depuis le démarrage)
            public int TotalPackets { get; set; }
            public long TotalBytes { get; set; }

            // Dernier état utile
            public int LastActiveChannels { get; set; }
            public DateTime LastSentUtc { get; set; }

            // Compteurs "en cours" pour la seconde actuelle
            public int TicksThisSecond { get; set; }

            public UniverseTxStats(int universe) => Universe = universe;

            /// <summary>
            /// Convertit l'état courant en snapshot "lisible".
            /// Toutes les ~1s, on "flip" les accumulateurs vers pps/bps puis on remet à zéro.
            /// </summary>
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

            /// <summary>
            /// Ajoute le nombre d'octets envoyés durant le "tick" courant.
            /// </summary>
            public void AddBytesThisTick(int bytes) => _bytesThisSecond += bytes;
        }

        /// <summary>
        /// DTO immuable exposé à l'UI pour affichage.
        /// </summary>
        public sealed class UniverseTxSnapshot
        {
            public int Universe { get; set; }
            public string TargetIP { get; set; } = string.Empty;
            public int PacketRatePerSec { get; set; }     // paquets/s
            public int ByteRatePerSec { get; set; }       // octets/s
            public int LastActiveChannels { get; set; }   // canaux non-zéro lors de la dernière trame
            public DateTime LastSentLocal { get; set; }   // horodatage local pour debug
        }
    }
}
