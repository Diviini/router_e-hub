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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;


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

        // [E5] --- Moniteur DMX (liste univers) ---
        [ObservableProperty] private bool showActiveUniversesOnly = true;
        public ObservableCollection<UniverseRow> UniverseRows { get; } = new();

        [ObservableProperty] private int totalPps; // paquets/s total
        [ObservableProperty] private int totalBps; // octets/s total


        // [E8] --- Patch Map ---
        [ObservableProperty] private bool isPatchEnabled;      // toggle appliquer/ignorer
        [ObservableProperty] private string? patchFilePath;
        [ObservableProperty] private int patchRuleCount;

        public ObservableCollection<PatchRuleRow> PatchRules { get; } = new();

        // commande : Charger un CSV de patch
        [RelayCommand]
        private async Task LoadPatchCsvAsync()
        {
            try
            {
                var window = GetMainWindow();
                if (window is null) return;

                var ofd = new OpenFileDialog
                {
                    Title = "Charger un CSV de Patch Map",
                    AllowMultiple = false,
                    Filters = new List<FileDialogFilter>
                    {
                        new FileDialogFilter { Name = "CSV", Extensions = { "csv" } },
                        new FileDialogFilter { Name = "Tous les fichiers", Extensions = { "*" } }
                    }
                };

                var res = await ofd.ShowAsync(window);
                
                if (res is null || res.Length == 0) return;

                var path = res[0];
                var map  = CsvPatchLoader.Load(path);

                // appliquer côté router mais sans activer automatiquement
                _router.SetPatchMap(map);
                PatchFilePath  = path;
                PatchRuleCount = map.Rules.Count;

                // remplir l'UI
                PatchRules.Clear();
                foreach (var r in map.Rules)
                {
                    PatchRules.Add(new PatchRuleRow
                    {
                        SrcUniverse = r.SrcUniverse,
                        SrcChannel  = r.SrcChannel,
                        DstUniverse = r.DstUniverse,
                        DstChannel  = r.DstChannel
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur chargement Patch CSV : {ex.Message}");
            }
        }

        // toggle appliquer/ignorer (ON = appliqué)
        partial void OnIsPatchEnabledChanged(bool value)
        {
            _router.EnablePatch(value);
        }

        // helper pour récupérer la fenêtre²
        private static Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        // Row pour DataGrid
        public class PatchRuleRow
        {
            public int SrcUniverse { get; set; }
            public int SrcChannel  { get; set; }
            public int DstUniverse { get; set; }
            public int DstChannel  { get; set; }
        }



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

                // [E5] récupérer snapshot DMX
                int pps, bps;
                var rows = _sender.GetStatsSnapshot(ShowActiveUniversesOnly, out pps, out bps);

                TotalPps = pps;
                TotalBps = bps;

                // Sync minimal (remplace tout : simple et suffisant à 4 Hz)
                UniverseRows.Clear();
                foreach (var r in rows)
                {
                    UniverseRows.Add(new UniverseRow
                    {
                        Universe = r.Universe,
                        TargetIP = r.TargetIP,
                        PacketRatePerSec = r.PacketRatePerSec,
                        ByteRatePerSec = r.ByteRatePerSec,
                        LastActiveChannels = r.LastActiveChannels,
                        LastSent = r.LastSentLocal.ToString("HH:mm:ss")
                    });
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
    
    // [E5] Row pour le tableau
    public class UniverseRow
    {
        public int Universe { get; set; }
        public string TargetIP { get; set; } = string.Empty;
        public int PacketRatePerSec { get; set; }
        public int ByteRatePerSec { get; set; }
        public int LastActiveChannels { get; set; }
        public string LastSent { get; set; } = "";
    }
}
