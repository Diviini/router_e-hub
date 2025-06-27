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
    int MaxFps = 40;

    private readonly CancellationTokenSource _cancellation = new();
    private Task? _routingLoop;

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
        var loopStart = DateTime.Now;

        Console.WriteLine("Démarrage du router...");
        await _receiver.StartAsync();

        _routingLoop = Task.Run(async () =>
        {
            while (!_cancellation.Token.IsCancellationRequested)
            {
                try
                {
                    var loopStart = DateTime.UtcNow;

                    // Mise à jour des entités
                    var entities = _receiver.GetCurrentEntities();
                    _mapper.UpdateEntities(entities);

                    // Récupération des frames actives
                    var frames = _mapper.GetActiveFrames();

                    var sendTasks = new List<Task>();

                    foreach (var frame in frames)
                    {
                        if (frame.HasChangedSinceLastSend())
                        {
                            // LogDmxFrameToFile(frame);
                            sendTasks.Add(_sender.SendDmxFrameAsync(frame));
                            frame.MarkAsSent(); // Marquer comme envoyée
                        }
                    }

                    await Task.WhenAll(sendTasks);

                    // Framerate limité (ex. : 40 FPS → 25 ms/frame)
                    var elapsed = DateTime.UtcNow - loopStart;
                    int remainingMs = Math.Max(0, 1000 / MaxFps - (int)elapsed.TotalMilliseconds);
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
    /// Arrête proprement le routeur
    /// </summary>
    public async Task StopAsync()
    {
        Console.WriteLine("Arrêt du router...");
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

}