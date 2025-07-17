using EmitterHub.DMX;
using EmitterHub.eHub;
using EmitterHub.ArtNet;

namespace EmitterHub.Routing;

/// <summary>
/// Gère la réception des messages eHuB, le mapping des entités vers DMX, et l'envoi ArtNet
/// </summary>
public class Router
{
    private readonly EHubReceiver _receiver;
    private readonly ArtNetSender _sender;
    private readonly DmxMapper _mapper;

    private CancellationTokenSource? _cancellation;
    private Task? _routingLoop;

    // Variables pour le logging des statistiques
    private int _tickCount = 0;
    private DateTime _lastLogTime = DateTime.Now;
    private int _framesSentThisSecond = 0;

    public Router(EHubReceiver receiver, ArtNetSender sender)
    {
        _receiver = receiver;
        _sender = sender;
        _mapper = new DmxMapper();

        _receiver.EntitiesUpdated += OnEntitiesUpdated;
    }

    /// <summary>
    /// Ajoute une plage d'entités à router
    /// </summary>
    /// <summary>
    /// Ajoute une plage d'entités avec mode DMX personnalisé et canal de départ
    /// </summary>
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
    /// Démarre l'écoute et le routage en tâche de fond
    /// </summary>
    public async Task StartAsync()
    {
        // Si déjà démarré, on ne réouvre pas
        if (_routingLoop != null && !_routingLoop.IsCompleted)
            return;

        _cancellation = new CancellationTokenSource();
        _receiver.EntitiesUpdated += OnEntitiesUpdated;

        Console.WriteLine("Démarrage du router...");
        await _receiver.StartAsync();

        _lastLogTime = DateTime.Now;

        _routingLoop = Task.Run(async () =>
        {
            while (!_cancellation.Token.IsCancellationRequested)
            {
                try
                {
                    var loopStart = DateTime.Now;

                    // Mettre à jour les entités (update)
                    var entities = _receiver.GetCurrentEntities();
                    _mapper.UpdateEntities(entities);

                    // Récupérer toutes les trames avec données
                    var frames = _mapper.GetAllFrames();

                    var sendTasks = frames.Select(frame =>
                    {
                        // LogDmxFrameToFile(frame);
                        return _sender.SendDmxFrameAsync(frame);
                    });
                    await Task.WhenAll(sendTasks);

                    // Compter les frames envoyées cette seconde
                    _framesSentThisSecond += frames.Count();

                    // Vérifier si une seconde s'est écoulée pour afficher les stats
                    if ((DateTime.Now - _lastLogTime).TotalSeconds >= 1.0)
                    {
                        _tickCount++;
                        Console.WriteLine($"[TICK {_tickCount}] eHuB Msgs: {_receiver.MessagesReceived} | Frames: {_framesSentThisSecond} | ArtNet sent: {_framesSentThisSecond} | Total: {_sender.PacketsSent}");

                        // Reset pour la prochaine seconde
                        _framesSentThisSecond = 0;
                        _lastLogTime = DateTime.Now;
                    }

                    // Attendre 25ms avant d'envoyer la prochaine série
                    var elapsed = DateTime.Now - loopStart;
                    int remainingMs = Math.Max(0, 25 - (int)elapsed.TotalMilliseconds);
                    await Task.Delay(remainingMs, _cancellation.Token);
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur dans la boucle de routage : {ex.Message}");
                }
            }
        });

        Console.WriteLine("Boucle de routage en cours...");
    }

    /// <summary>
    /// Renvoie la liste des univers pour lesquels on a créé des trames DMX
    /// </summary>
    public IEnumerable<int> GetConfiguredUniverses()
        => _mapper.GetAllFrames().Select(f => f.Universe);

    /// <summary>
    /// Arrête proprement le routeur
    /// </summary>
    public async Task StopAsync()
    {
        Console.WriteLine("Arrêt du router...");
        _cancellation.Cancel();
        if (_cancellation != null)
            _cancellation.Cancel();
        _receiver.Stop();

        if (_routingLoop != null)
            await _routingLoop;
    }

    /// <summary>
    /// Callback interne lors d'une mise à jour eHuB
    /// </summary>
    private void OnEntitiesUpdated(Dictionary<ushort, EntityState> updated)
    {
        _mapper.UpdateEntities(updated);
    }

    private void LogDmxFrameToFile(DmxFrame frame)
    {
        string folder = "Logs";
        string file = Path.Combine(folder, "dmx_log.txt");

        try
        {
            Directory.CreateDirectory(folder); // Crée le dossier Logs s'il n'existe pas

            using StreamWriter sw = new StreamWriter(file, append: true);

            // En-tête : date + cible
            sw.WriteLine($"[DMX] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - Univers {frame.Universe} - IP: {frame.TargetIP}");

            // Log des canaux actifs uniquement
            for (int i = 1; i <= DmxFrame.DMX_CHANNELS; i++)
            {
                byte value = frame.GetChannel(i);
                if (value > 0)
                {
                    sw.WriteLine($"  Canal {i} : {value}");
                }
            }

            sw.WriteLine(); // Ligne vide pour séparer les trames
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors du log DMX : {ex.Message}");
        }
    }

    public MappingStats GetStats()
    {
        return _mapper.GetStats();
    }
}