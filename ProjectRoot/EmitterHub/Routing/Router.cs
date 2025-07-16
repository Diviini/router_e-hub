using EmitterHub.DMX;
using EmitterHub.eHub;
using EmitterHub.ArtNet;

namespace EmitterHub.Routing;

public class Router
{
    private readonly EHubReceiver _receiver;
    private readonly ArtNetSender _sender;
    private readonly DmxMapper _mapper;
    private int _tickCount = 0;

    public Router(EHubReceiver receiver, ArtNetSender sender)
    {
        _receiver = receiver;
        _sender = new ArtNetSender { SendAllFrames = true }; // Forcer l'envoi de toutes les trames
        _mapper = new DmxMapper();
    }

    public void AddEntityRange(ushort entityStart, ushort entityEnd, string ip, ushort universeStart, ushort universeEnd, string channelMode, ushort dmxStartChannel)
    {
        _mapper.AddEntityRangeMapping(entityStart, entityEnd, ip, universeStart, universeEnd, channelMode, dmxStartChannel);
    }

    public void Tick()
    {
        _tickCount++;
        _receiver.TryReceiveOnce();

        var entities = _receiver.GetCurrentEntities();
        _mapper.UpdateEntities(entities);
        var frames = _mapper.GetAllFrames().ToList();

        Parallel.ForEach(frames, frame =>
        {
            _sender.SendDmxFrameAsync(frame).Wait();
        });

        if (_tickCount % 40 == 0)
        {
            Console.WriteLine($"[TICK {_tickCount}] eHuB Msgs: {_receiver.MessagesReceived} | Frames: {frames.Count} | ArtNet sent: {frames.Count} | Total: {_sender.PacketsSent}");
        }
    }


    public int GetMessageCount() => _receiver.MessagesReceived;
    public int GetEntityCount() => _receiver.ActiveEntities;
    public int GetPacketsSent() => _sender.PacketsSent;
    public Dictionary<ushort, ushort> GetIndexMap() => _receiver.GetIndexToEntityMapping();
}

