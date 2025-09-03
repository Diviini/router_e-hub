using System;
using System.Timers;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using EmitterHub.eHub;
using EmitterHub.ArtNet;
using EmitterHub.Routing;
using EmitterHub.DMX;

namespace EmitterHub.UI.ViewModels
{
    /// <summary>
    /// ViewModel principal de l'UI. 
    /// - Centralise les statistiques
    /// - Active les moniteurs (eHuB, DMX, ArtNet)
    /// - Gère le PatchMap
    /// - Fournit un Faker (plein écran) pour tester sans matériel
    /// </summary>
    public partial class StatsViewModel : ObservableObject
    {
        // ===================== Dépendances =====================
        private readonly Router _router;            // Responsable du routage entités -> DMX
        private readonly EHubReceiver _receiver;    // Récepteur eHuB (messages Unity/Faker)
        private readonly ArtNetSender _sender;      // Émetteur ArtNet
        private readonly DispatcherTimer _statsTimer;
        // ===================== Etats internes =====================
        private FrameInfo? _pendingFrame;           // Dernière trame DMX en attente (pour le moniteur instantané)
        private int _prevMsgCount = 0;              // Pour calcul du FPS eHuB
        private const int EhUbHistorySize = 120;    // Historique eHuB (30s à 4Hz)

        // ===================== [E9] Moniteur ArtNet =====================
        private readonly ArtNetListener _artnetListener = new();                    // Écoute UDP 6454
        private readonly ConcurrentDictionary<int, ArtnetFrameRow> _artnetLatest = new(); // Dernier paquet reçu par univers

        [ObservableProperty] private bool isArtnetMonitorEnabled;   // Toggle ON/OFF
        public ObservableCollection<ArtnetFrameRow> ArtnetFrames { get; } = new(); // Snapshot affiché dans l’UI

        // ===================== [E10] Faker plein écran =====================
        private readonly EHubFaker _fullFaker = new();

        [ObservableProperty] private bool isFullFakerEnabled;   // Toggle ON/OFF du faker
        [ObservableProperty] private byte colorR = 255;         // Valeur Rouge par défaut
        [ObservableProperty] private byte colorG = 255;         // Valeur Verte par défaut
        [ObservableProperty] private byte colorB = 255;         // Valeur Bleue par défaut

        // ===================== [E2] Stats générales =====================
        [ObservableProperty] private int messagesReceived;      // Nombre total de messages eHuB reçus
        [ObservableProperty] private int activeEntities;        // Nombre d’entités actives
        [ObservableProperty] private int packetsSent;           // Nombre de paquets ArtNet envoyés
        [ObservableProperty] private int totalUniverses;        // Nombre total d’univers DMX gérés
        [ObservableProperty] private int totalMappings;         // Nombre d’entités mappées
        [ObservableProperty] private int activeFrames;          // Nombre de trames DMX actives

        // ===================== [E2] Moniteur eHuB =====================
        [ObservableProperty] private bool isEhubMonitorEnabled; // Toggle ON/OFF
        [ObservableProperty] private int ehubFps;               // Messages/s calculé
        [ObservableProperty] private int ehubFpsRaw;      // valeur réelle, non bornée
        [ObservableProperty] private int ehubFpsDisplay;  // valeur bornée pour l’UI
        public ObservableCollection<int> EhubFpsHistory { get; } = new(); // Historique du FPS

        // ===================== [E5] Moniteur DMX (sortie ArtNet) =====================
        [ObservableProperty] private bool showActiveUniversesOnly = true; // Filtre ON/OFF
        public ObservableCollection<UniverseRow> UniverseRows { get; } = new();

        [ObservableProperty] private int totalPps;  // Paquets/s total
        [ObservableProperty] private int totalBps;  // Octets/s total

        // ===================== Moniteur DMX (par univers) =====================
        [ObservableProperty] private bool isMonitorEnabled;     // Toggle moniteur instantané ON/OFF
        [ObservableProperty] private List<int> universeOptions = new();
        [ObservableProperty] private int selectedUniverse;      // Univers sélectionné dans l’UI
        [ObservableProperty] private FrameInfo? currentFrame;   // Trame courante (snapshot)

        // ===================== [E8] Patch Map =====================
        [ObservableProperty] private bool isPatchEnabled;       // Toggle appliquer/ignorer
        [ObservableProperty] private string? patchFilePath;     // Fichier CSV chargé
        [ObservableProperty] private int patchRuleCount;        // Nombre de règles
        public ObservableCollection<PatchRuleRow> PatchRules { get; } = new();

        // ===================== Constructeur =====================

        public StatsViewModel(Router router, EHubReceiver receiver, ArtNetSender sender)
        {
            _router = router;
            _receiver = receiver;
            _sender = sender;

            // Univers disponibles
            UniverseOptions = _router.GetConfiguredUniverses().ToList();
            if (UniverseOptions.Any())
                SelectedUniverse = UniverseOptions.First();

            // Timer pour refresh UI (250ms = 4Hz)
            _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _statsTimer.Tick += (_, __) => Refresh();
            _statsTimer.Start();

            // Abonnements
            _sender.FrameSent += OnFrameSent;                  // Envoi DMX
            _artnetListener.FrameReceived += OnArtnetFrameReceived; // Réception ArtNet
        }

        // ===================== [E9] Gestion Moniteur ArtNet =====================
        private void OnArtnetFrameReceived(ArtnetFrameRow row)
        {
            Dispatcher.UIThread.Post(() =>
            {
                // On garde seulement le dernier paquet par univers
                _artnetLatest[row.Universe] = row;
            });
        }

        partial void OnIsArtnetMonitorEnabledChanged(bool value)
        {
            if (value) _artnetListener.Start();
            else _artnetListener.Stop();
        }

        // ===================== [E10] Gestion Faker plein écran =====================
        partial void OnIsFullFakerEnabledChanged(bool value)
        {
            if (value)
            {
                // Charger les plages d’entités depuis le Router
                var ranges = _router.GetEntityRanges();
                _fullFaker.SetRangesFromRouter(ranges);

                // Forcer l’univers eHuB (doit matcher Receiver)
                _fullFaker.EhubUniverse = 1;

                // Appliquer la couleur courante
                _fullFaker.SolidR = ColorR;
                _fullFaker.SolidG = ColorG;
                _fullFaker.SolidB = ColorB;

                _fullFaker.Start();
            }
            else
            {
                _fullFaker.Stop();
            }
        }

        [RelayCommand]
        private void ApplySolidColor()
        {
            _fullFaker.SolidR = ColorR;
            _fullFaker.SolidG = ColorG;
            _fullFaker.SolidB = ColorB;
        }

        // ===================== [E8] Gestion Patch Map =====================
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
                var map = CsvPatchLoader.Load(path);

                _router.SetPatchMap(map);   // On enregistre le patch dans le router
                PatchFilePath = path;
                PatchRuleCount = map.Rules.Count;

                // Mise à jour de l’UI
                PatchRules.Clear();
                foreach (var r in map.Rules)
                {
                    PatchRules.Add(new PatchRuleRow
                    {
                        SrcUniverse = r.SrcUniverse,
                        SrcChannel = r.SrcChannel,
                        DstUniverse = r.DstUniverse,
                        DstChannel = r.DstChannel
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur chargement Patch CSV : {ex.Message}");
            }
        }

        partial void OnIsPatchEnabledChanged(bool value)
        {
            _router.EnablePatch(value);
        }

        // ===================== Gestion Moniteur DMX =====================
        partial void OnIsMonitorEnabledChanged(bool value)
        {
            if (!value)
                CurrentFrame = null;
        }

        partial void OnSelectedUniverseChanged(int value)
        {
            CurrentFrame = null;
        }

        private void OnFrameSent(DmxFrame frame)
        {
            if (!IsMonitorEnabled || frame.Universe != SelectedUniverse)
                return;

            // On clone les canaux pour éviter tout problème de thread-safety
            var channels = frame.Channels.ToArray();
            _pendingFrame = new FrameInfo(
                frame.Universe,
                frame.TargetIP,
                channels.Count(b => b > 0),
                channels
            );
        }

        // ===================== Refresh global (toutes les 250ms) =====================
        private void Refresh()
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Stats générales
                MessagesReceived = _receiver.MessagesReceived;
                ActiveEntities = _receiver.ActiveEntities;
                PacketsSent = _sender.PacketsSent;

                var stats = _router.GetStats();
                TotalUniverses = stats.TotalUniverses;
                TotalMappings = stats.TotalEntities;
                ActiveFrames = stats.ActiveFrames;

                // [E2] FPS eHuB
                if (IsEhubMonitorEnabled)
                {
                    int cur = _receiver.MessagesReceived;

                    int delta = Math.Max(0, cur - _prevMsgCount);
                    _prevMsgCount = cur;
                    int fpsRaw = (int)Math.Round(delta * (1000.0 / _statsTimer.Interval.TotalMilliseconds)); // non borné
                    EhubFpsRaw = fpsRaw;
                    
                    int fpsDisplay = Math.Clamp(fpsRaw, 0, 60); // juste pour la sparkline
                    EhubFpsDisplay = fpsDisplay;

                    // Historique pour la sparkline → on pousse la version bornée
                    if (EhubFpsHistory.Count >= EhUbHistorySize) EhubFpsHistory.RemoveAt(0);
                    EhubFpsHistory.Add(fpsRaw);


                }
                else
                {
                    _prevMsgCount = _receiver.MessagesReceived;
                }

                // [E5] Snapshot DMX par univers
                int pps, bps;
                var rows = _sender.GetStatsSnapshot(ShowActiveUniversesOnly, out pps, out bps);
                TotalPps = pps;
                TotalBps = bps;

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

                // Moniteur DMX instantané
                if (IsMonitorEnabled)
                    CurrentFrame = _pendingFrame;

                // [E9] Snapshot ArtNet
                if (IsArtnetMonitorEnabled)
                {
                    var snapshot = _artnetLatest.Values.OrderBy(v => v.Universe).ToList();
                    ArtnetFrames.Clear();
                    foreach (var r in snapshot) ArtnetFrames.Add(r);
                }
            });
        }

        // ===================== Commandes =====================
        [RelayCommand]
        private async Task StopRouterAsync()
        {
            _statsTimer.Stop();
            _artnetListener.Stop();
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

        // ===================== Helpers =====================
        private static Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }
    }

    // ===================== Structures auxiliaires =====================
    public record FrameInfo(int Universe, string TargetIP, int ActiveChannels, byte[] Channels)
    {
        public string DisplayText => $"U{Universe} → {TargetIP} ({ActiveChannels} canaux)";
    }

    public class UniverseRow
    {
        public int Universe { get; set; }
        public string TargetIP { get; set; } = string.Empty;
        public int PacketRatePerSec { get; set; }
        public int ByteRatePerSec { get; set; }
        public int LastActiveChannels { get; set; }
        public string LastSent { get; set; } = "";
    }

    public class PatchRuleRow
    {
        public int SrcUniverse { get; set; }
        public int SrcChannel { get; set; }
        public int DstUniverse { get; set; }
        public int DstChannel { get; set; }
    }
}
