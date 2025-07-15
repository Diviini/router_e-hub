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
    private readonly int _maxFps = 40;

    private readonly CancellationTokenSource _cancellation = new();
    private Task? _routingLoop;

    public Router(EHubReceiver receiver, ArtNetSender sender)
    {
        _receiver = receiver;
        _sender = sender;
        _mapper = new DmxMapper();

        _receiver.EntitiesUpdated += OnEntitiesUpdated;
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

    public async Task StartAsync()
    {
        Console.WriteLine("Démarrage du router...");
        await _receiver.StartAsync();

        _routingLoop = Task.Run(RoutingLoop, _cancellation.Token);

        Console.WriteLine("Boucle de routage en cours...");
    }

    private async Task RoutingLoop()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / _maxFps));
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 8, // Limite le nombre d'envois simultanés
            CancellationToken = _cancellation.Token
        };

        while (await timer.WaitForNextTickAsync(_cancellation.Token))
        {
            try
            {
                var frames = _mapper.GetModifiedFrames();
                await Parallel.ForEachAsync(frames, parallelOptions, async (frame, token) =>
                {
                    await _sender.SendDmxFrameAsync(frame);
                });
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur dans la boucle de routage : {ex.Message}");
            }
        }
    }

    public async Task StopAsync()
    {
        Console.WriteLine("Arrêt du router...");
        _cancellation.Cancel();
        _receiver.Stop();

        if (_routingLoop != null)
            await _routingLoop;
    }

    private void OnEntitiesUpdated(Dictionary<ushort, EntityState> updated)
    {
        _mapper.UpdateEntities(updated);
    }
}