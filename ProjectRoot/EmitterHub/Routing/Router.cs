using EmitterHub.DMX;
using EmitterHub.eHub;
using EmitterHub.ArtNet;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace EmitterHub.Routing;

/// <summary>
/// Router optimisé pour éviter les goulots d'étranglement réseau
/// </summary>
public class Router
{
    private readonly EHubReceiver _receiver;
    private readonly ArtNetSender _sender;
    private readonly DmxMapper _mapper;

    // Configuration réseau optimisée
    private readonly int _maxFps = 25; // Réduit de 40 à 25 FPS
    private readonly int _packetDelay = 2; // 2ms entre chaque paquet
    private readonly int _syncInterval = 1000; // Sync complète toutes les 1000ms

    // Gestion de la bande passante
    private readonly ConcurrentQueue<DmxFrame> _sendQueue = new();
    private readonly SemaphoreSlim _networkSemaphore = new(3, 3); // Max 3 envois simultanés

    // Monitoring réseau
    private readonly Stopwatch _lastSyncTime = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastSentPerIP = new();
    private readonly ConcurrentDictionary<int, DmxFrame> _lastSentFrames = new();

    // Contrôle de flux
    private volatile bool _networkCongestion = false;
    private DateTime _lastCongestionCheck = DateTime.Now;

    private readonly CancellationTokenSource _cancellation = new();
    private Task? _routingLoop;
    private Task? _syncLoop;
    private Task? _networkMonitorLoop;

    public Router(EHubReceiver receiver, ArtNetSender sender)
    {
        _receiver = receiver;
        _sender = sender;
        _mapper = new DmxMapper();

        _receiver.EntitiesUpdated += OnEntitiesUpdated;
        _lastSyncTime.Start();
    }

    public void AddEntityRange(
        ushort entityStart,
        ushort entityEnd,
        string ip,
        ushort universeStart,
        ushort universeEnd,
        string channelMode,
        ushort dmxStartChannel)
    {
        _mapper.AddEntityRangeMapping(
            entityStart,
            entityEnd,
            ip,
            universeStart,
            universeEnd,
            channelMode,
            dmxStartChannel
        );
    }

    /// <summary>
    /// Démarre le routage avec optimisations réseau
    /// </summary>
    public async Task StartAsync()
    {
        Console.WriteLine("🚀 Démarrage du router optimisé...");
        await _receiver.StartAsync();

        // Boucle principale optimisée
        _routingLoop = Task.Run(OptimizedRoutingLoop);

        // Boucle de synchronisation périodique
        _syncLoop = Task.Run(SyncLoop);

        // Monitoring réseau
        _networkMonitorLoop = Task.Run(NetworkMonitorLoop);

        Console.WriteLine("✅ Router optimisé démarré - FPS max: {0}, Délai paquets: {1}ms", _maxFps, _packetDelay);
    }

    /// <summary>
    /// Boucle de routage optimisée avec contrôle de flux
    /// </summary>
    private async Task OptimizedRoutingLoop()
    {
        var frameInterval = TimeSpan.FromMilliseconds(1000.0 / _maxFps);
        var lastFrameTime = DateTime.Now;

        while (!_cancellation.Token.IsCancellationRequested)
        {
            try
            {
                var frameStart = DateTime.Now;

                // Ajustement dynamique selon la congestion
                if (_networkCongestion)
                {
                    await Task.Delay(50, _cancellation.Token); // Ralentir si congestion
                    continue;
                }

                // Récupération des entités
                var entities = _receiver.GetCurrentEntities();
                _mapper.UpdateEntities(entities);

                // Récupération des frames actives
                var activeFrames = _mapper.GetActiveFrames().ToList();

                // Envoi optimisé avec régulation
                await SendFramesWithThrottling(activeFrames);

                // Régulation FPS intelligente
                var elapsed = DateTime.Now - frameStart;
                var remainingTime = frameInterval - elapsed;

                if (remainingTime > TimeSpan.Zero)
                {
                    await Task.Delay(remainingTime, _cancellation.Token);
                }
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Erreur boucle routage : {0}", ex.Message);
                await Task.Delay(100, _cancellation.Token); // Pause avant retry
            }
        }
    }

    /// <summary>
    /// Envoi régulé avec gestion de la bande passante
    /// </summary>
    private async Task SendFramesWithThrottling(List<DmxFrame> frames)
    {
        var tasks = new List<Task>();

        foreach (var frame in frames)
        {
            // Éviter les envois trop fréquents vers la même IP
            if (_lastSentPerIP.TryGetValue(frame.TargetIP, out var lastSent))
            {
                if (DateTime.Now - lastSent < TimeSpan.FromMilliseconds(20)) // 20ms minimum entre envois
                {
                    continue;
                }
            }

            // Éviter les envois redondants
            if (_lastSentFrames.TryGetValue(frame.Universe, out var lastFrame))
            {
                if (AreFramesIdentical(frame, lastFrame))
                {
                    continue; // Skip si identique
                }
            }

            tasks.Add(SendSingleFrameThrottled(frame));

            // Limite le nombre de tâches simultanées
            if (tasks.Count >= 3)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// Envoi d'une frame avec contrôle de débit
    /// </summary>
    private async Task SendSingleFrameThrottled(DmxFrame frame)
    {
        await _networkSemaphore.WaitAsync(_cancellation.Token);

        try
        {
            // Délai entre paquets pour éviter les rafales
            await Task.Delay(_packetDelay, _cancellation.Token);

            await _sender.SendDmxFrameAsync(frame);

            // Mise à jour du cache
            _lastSentPerIP[frame.TargetIP] = DateTime.Now;
            _lastSentFrames[frame.Universe] = CloneFrame(frame);

            // Log asynchrone
            _ = Task.Run(() => LogDmxFrameToFileAsync(frame));
        }
        finally
        {
            _networkSemaphore.Release();
        }
    }

    /// <summary>
    /// Synchronisation périodique complète
    /// </summary>
    private async Task SyncLoop()
    {
        while (!_cancellation.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_syncInterval, _cancellation.Token);

                Console.WriteLine("🔄 Synchronisation complète...");

                // Envoi de toutes les frames (même vides) pour sync
                var allFrames = _mapper.GetAllFrames();
                await SendFullSync(allFrames);

                // Nettoyage du cache
                CleanupCache();
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Erreur sync : {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Envoi de synchronisation complète
    /// </summary>
    private async Task SendFullSync(IEnumerable<DmxFrame> frames)
    {
        var tasks = new List<Task>();

        foreach (var frame in frames)
        {
            tasks.Add(Task.Run(async () =>
            {
                await _networkSemaphore.WaitAsync(_cancellation.Token);
                try
                {
                    await Task.Delay(5, _cancellation.Token); // Délai plus court pour sync
                    await _sender.SendDmxFrameAsync(frame);
                }
                finally
                {
                    _networkSemaphore.Release();
                }
            }));

            // Batch par groupe de 5 pour éviter surcharge
            if (tasks.Count >= 5)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
                await Task.Delay(10, _cancellation.Token); // Pause entre batches
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// Monitoring de la congestion réseau
    /// </summary>
    private async Task NetworkMonitorLoop()
    {
        while (!_cancellation.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, _cancellation.Token); // Check toutes les 5 secondes

                // Vérification simple de congestion basée sur le taux d'envoi
                var now = DateTime.Now;
                var recentSends = _lastSentPerIP.Values.Count(t => now - t < TimeSpan.FromSeconds(1));

                _networkCongestion = recentSends > 100; // Plus de 100 envois/seconde = congestion

                if (_networkCongestion)
                {
                    Console.WriteLine("⚠️ Congestion réseau détectée - Ralentissement");
                }
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Erreur monitoring : {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Nettoyage périodique du cache
    /// </summary>
    private void CleanupCache()
    {
        var cutoff = DateTime.Now - TimeSpan.FromSeconds(5);

        var keysToRemove = _lastSentPerIP
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _lastSentPerIP.TryRemove(key, out _);
        }

    }

    /// <summary>
    /// Comparaison rapide de frames
    /// </summary>
    private bool AreFramesIdentical(DmxFrame frame1, DmxFrame frame2)
    {
        if (frame1.Universe != frame2.Universe || frame1.TargetIP != frame2.TargetIP)
            return false;

        return frame1.Channels.SequenceEqual(frame2.Channels);
    }

    /// <summary>
    /// Clone rapide d'une frame
    /// </summary>
    private DmxFrame CloneFrame(DmxFrame original)
    {
        var clone = new DmxFrame(original.Universe)
        {
            TargetIP = original.TargetIP
        };
        original.CopyTo(clone);
        return clone;
    }

    /// <summary>
    /// Log asynchrone pour éviter le blocage
    /// </summary>
    private async Task LogDmxFrameToFileAsync(DmxFrame frame)
    {
        try
        {
            string folder = "Logs";
            string file = Path.Combine(folder, $"dmx_log_{DateTime.Now:yyyyMMdd}.txt");

            await Task.Run(() =>
            {
                Directory.CreateDirectory(folder);

                using var sw = new StreamWriter(file, append: true);
                sw.WriteLine("[{0}] U{1} -> {2} ({3} active)",
                    DateTime.Now.ToString("HH:mm:ss.fff"),
                    frame.Universe,
                    frame.TargetIP,
                    frame.Channels.Count(c => c > 0));
            });
        }
        catch
        {
            // Ignore les erreurs de log pour ne pas impacter le réseau
        }
    }

    /// <summary>
    /// Callback interne lors d'une mise à jour eHuB
    /// </summary>
    private void OnEntitiesUpdated(Dictionary<ushort, EntityState> updated)
    {
        // Mise à jour non-bloquante
        _ = Task.Run(() => _mapper.UpdateEntities(updated));
    }

    /// <summary>
    /// Arrêt propre du routeur
    /// </summary>
    public async Task StopAsync()
    {
        Console.WriteLine("🛑 Arrêt du router optimisé...");
        _cancellation.Cancel();
        _receiver.Stop();

        var tasks = new[] { _routingLoop, _syncLoop, _networkMonitorLoop }
            .Where(t => t != null)
            .ToArray();

        await Task.WhenAll(tasks);

        _networkSemaphore.Dispose();
        Console.WriteLine("✅ Router arrêté proprement");
    }
}