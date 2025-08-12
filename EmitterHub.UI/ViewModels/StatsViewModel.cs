using System;
using System.Timers;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using EmitterHub.eHub;
using EmitterHub.ArtNet;
using EmitterHub.Routing;
using EmitterHub.DMX;

namespace EmitterHub.UI.ViewModels
{
    public partial class StatsViewModel : ObservableObject
    {
        private readonly Router _router;
        private readonly EHubReceiver _receiver;
        private readonly ArtNetSender _sender;
        private readonly Timer _statsTimer;

        // Stocke la dernière trame reçue
        private FrameInfo? _pendingFrame;

        // [E2] --- Champs internes pour FPS eHuB ---
        private int _prevMsgCount = 0;
        private const int EhUbHistorySize = 120; // ~30s à 4Hz

        public StatsViewModel(Router router, EHubReceiver receiver, ArtNetSender sender)
        {
            _router = router;
            _receiver = receiver;
            _sender = sender;

            // Initialisation des univers configurés
            UniverseOptions = _router.GetConfiguredUniverses().ToList();
            if (UniverseOptions.Any())
                SelectedUniverse = UniverseOptions.First();

            // Timer pour rafraîchir les statistiques et la vue du moniteur à 4 Hz
            _statsTimer = new Timer(250);
            _statsTimer.Elapsed += (_, __) => Refresh();
            _statsTimer.Start();

            // Souscription aux trames
            _sender.FrameSent += OnFrameSent;
        }

        // --- Propriétés de statistiques ---
        [ObservableProperty] private int messagesReceived;
        [ObservableProperty] private int activeEntities;
        [ObservableProperty] private int packetsSent;
        [ObservableProperty] private int totalUniverses;
        [ObservableProperty] private int totalMappings;
        [ObservableProperty] private int activeFrames;

        // --- Propriétés du moniteur ---
        [ObservableProperty] private bool isMonitorEnabled;
        [ObservableProperty] private List<int> universeOptions = new();
        [ObservableProperty] private int selectedUniverse;
        [ObservableProperty] private FrameInfo? currentFrame;

         // [E2] --- Moniteur eHuB ---
        [ObservableProperty] private bool isEhubMonitorEnabled;     // toggle
        [ObservableProperty] private int ehubFps;                   // FPS instantané (messages/s)
        public ObservableCollection<int> EhubFpsHistory { get; } = new(); // historique borné 0..60


        partial void OnIsMonitorEnabledChanged(bool value)
        {
            if (!value)
                CurrentFrame = null;
        }

        partial void OnSelectedUniverseChanged(int value)
        {
            CurrentFrame = null;
        }

        // Capture la trame reçue mais ne met pas à jour l'UI immédiatement
        private void OnFrameSent(DmxFrame frame)
        {
            if (!IsMonitorEnabled || frame.Universe != SelectedUniverse)
                return;

            // Clone pour thread-safety
            var channels = frame.Channels.ToArray();
            _pendingFrame = new FrameInfo(
                frame.Universe,
                frame.TargetIP,
                channels.Count(b => b > 0),
                channels
            );
        }

        // Met à jour stats et moniteur à intervalle régulier sur le thread UI
        private void Refresh()
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Statistiques générales
                MessagesReceived = _receiver.MessagesReceived;
                ActiveEntities = _receiver.ActiveEntities;
                PacketsSent = _sender.PacketsSent;

                var stats = _router.GetStats();
                TotalUniverses = stats.TotalUniverses;
                TotalMappings = stats.TotalEntities;
                ActiveFrames = stats.ActiveFrames;

                // [E2] Calcul FPS eHuB + historique (uniquement si activé)
                if (IsEhubMonitorEnabled)
                {
                    int cur = _receiver.MessagesReceived;
                    int delta = Math.Max(0, cur - _prevMsgCount);
                    _prevMsgCount = cur;

                    // timer = 250ms -> *4 pour messages/s
                    int fps = (int)Math.Round(delta * (1000.0 / _statsTimer.Interval));
                    // borne visuelle 0..60 pour notre graphe vertical de 60px
                    fps = Math.Clamp(fps, 0, 60);

                    EhubFps = fps;

                    if (EhubFpsHistory.Count >= EhUbHistorySize)
                        EhubFpsHistory.RemoveAt(0);
                    EhubFpsHistory.Add(fps);
                }
                else
                {
                    // quand OFF, on ne fait rien et on fige l'historique
                    _prevMsgCount = _receiver.MessagesReceived;
                }

                // Mise à jour du moniteur en temps réel
                if (IsMonitorEnabled)
                {
                    CurrentFrame = _pendingFrame;
                }
            });
        }

        [RelayCommand]
        private async Task StopRouterAsync()
        {
            _statsTimer.Stop();
            await _router.StopAsync();
        }

        [RelayCommand]
        private async Task RestartRouterAsync()
        {
            _statsTimer.Stop();
            await _router.StopAsync();
            await _router.StartAsync();
            _statsTimer.Start();
        }

        [RelayCommand]
        private void ClearMonitor()
        {
            CurrentFrame = null;
            _pendingFrame = null;
        }
    }

    public record FrameInfo(int Universe, string TargetIP, int ActiveChannels, byte[] Channels)
    {
        public string DisplayText => $"U{Universe} → {TargetIP} ({ActiveChannels} canaux)";
    }
}
