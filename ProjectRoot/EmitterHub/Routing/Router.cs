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
        Console.WriteLine("Démarrage du router...");
        await _receiver.StartAsync();

        _routingLoop = Task.Run(async () =>
        {
            while (!_cancellation.Token.IsCancellationRequested)
            {
                try
                {
                    // Met à jour les trames DMX à partir des entités reçues
                    var entities = _receiver.GetCurrentEntities();
                    _mapper.UpdateEntities(entities);

                    // Envoie les trames DMX actives via ArtNet
                    foreach (var frame in _mapper.GetActiveFrames())
                    {
                        await _sender.SendDmxFrameAsync(frame);
                    }

                    await Task.Delay(25, _cancellation.Token); // 40 FPS max
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
}