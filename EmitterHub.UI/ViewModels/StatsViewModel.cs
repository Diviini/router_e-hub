using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmitterHub.eHub;
using EmitterHub.ArtNet;
using EmitterHub.Routing;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace EmitterHub.UI.ViewModels
{
    public partial class StatsViewModel : ObservableObject
    {
        private readonly Router _router;
        private readonly EHubReceiver _receiver;
        private readonly ArtNetSender _sender;
        private readonly Timer _timer;

        public StatsViewModel(Router router, EHubReceiver receiver, ArtNetSender sender)
        {
            _router = router;
            _receiver = receiver;
            _sender = sender;

            // Timer pour rafraîchir toutes les 250 ms
            _timer = new Timer(250);
            _timer.Elapsed += (_, __) => Refresh();
            _timer.Start();
        }

        // Propriétés bindées à la vue
        [ObservableProperty] private int messagesReceived;
        [ObservableProperty] private int activeEntities;
        [ObservableProperty] private int packetsSent;
        [ObservableProperty] private int totalUniverses;
        [ObservableProperty] private int totalMappings;
        [ObservableProperty] private int activeFrames;

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
    }
}
