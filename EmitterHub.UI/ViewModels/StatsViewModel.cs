using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmitterHub.eHub;
using EmitterHub.ArtNet;
using EmitterHub.Routing;
using System.Threading.Tasks;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using EmitterHub.DMX;
using System.Linq;
using System.Collections.Generic;

namespace EmitterHub.UI.ViewModels
{
    public partial class StatsViewModel : ObservableObject
    {
        private readonly Router _router;
        private readonly EHubReceiver _receiver;
        private readonly ArtNetSender _sender;
        private readonly Timer _timer;
        private bool _frameUpdatePending = false;
        private FrameInfo? _lastFrame;

        // ➊ Collection pour afficher les trames
        public ObservableCollection<FrameInfo> SentFrames { get; } = new();




        public StatsViewModel(Router router, EHubReceiver receiver, ArtNetSender sender)
        {
            _router = router;
            _receiver = receiver;
            _sender = sender;

            UniverseOptions = router.GetConfiguredUniverses().ToList();
            if (UniverseOptions.Any())
                SelectedUniverse = UniverseOptions.First();

            // Timer pour rafraîchir toutes les 250 ms
            _timer = new Timer(250);
            _timer.Elapsed += (_, __) => Refresh();
            _timer.Start();

            sender.FrameSent += OnFrameSent;
        }
        // ➍ Quand on change d'univers, on vide le moniteur
        partial void OnSelectedUniverseChanged(int value)
            => SentFrames.Clear();

        // Propriétés bindées à la vue
        [ObservableProperty] private int messagesReceived;
        [ObservableProperty] private int activeEntities;
        [ObservableProperty] private int packetsSent;
        [ObservableProperty] private int totalUniverses;
        [ObservableProperty] private int totalMappings;
        [ObservableProperty] private int activeFrames;
        [ObservableProperty] private bool isMonitorEnabled = false;
        [ObservableProperty] private List<int> universeOptions = new();
        [ObservableProperty] private int selectedUniverse;

        partial void OnIsMonitorEnabledChanged(bool value)
        {
            if (!value)
                SentFrames.Clear();
        }
        private void OnFrameSent(DmxFrame frame)
        {
            if (!IsMonitorEnabled)
                return;

            if (frame.Universe != SelectedUniverse)
                return;

            // Préparer la dernière frame
            _lastFrame = new FrameInfo(
                frame.Universe,
                frame.TargetIP,
                frame.Channels.Count(b => b > 0)
            );

            // Si une mise à jour UI est déjà en attente, on sort
            if (_frameUpdatePending)
                return;

            _frameUpdatePending = true;
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (_lastFrame is not null)
                    {
                        SentFrames.Add(_lastFrame);
                        if (SentFrames.Count > 200)
                            SentFrames.RemoveAt(0);
                    }
                }
                finally
                {
                    _frameUpdatePending = false;
                }
            });
        }

        // Méthode de rafraîchissement des valeurs
        private void Refresh()
        {
            Dispatcher.UIThread.Post(() =>
            {
                MessagesReceived = _receiver.MessagesReceived;
                ActiveEntities = _receiver.ActiveEntities;
                PacketsSent = _sender.PacketsSent;

                var stats = _router.GetStats();
                TotalUniverses = stats.TotalUniverses;
                TotalMappings = stats.TotalEntities;
                ActiveFrames = stats.ActiveFrames;
            });
        }

        // Commande pour arrêter proprement le router
        [RelayCommand]
        private async Task StopRouterAsync()
        {
            _timer.Stop();
            await _router.StopAsync();
        }

        [RelayCommand]
        private async Task RestartRouterAsync()
        {
            _timer.Stop();
            await _router.StopAsync();
            await _router.StartAsync();
            _timer.Start();
        }

        // ➍ Commande pour vider le moniteur
        [RelayCommand]
        private void ClearMonitor()
        {
            SentFrames.Clear();
        }

    }

    // Si tu l'avais imbriqué dans StatsViewModel, tu peux aussi le sortir au niveau du namespace
    public record FrameInfo(int Universe, string TargetIP, int ActiveChannels)
    {
        public string DisplayText =>
            $"U{Universe} → {TargetIP} ({ActiveChannels} canaux)";
    }
}
