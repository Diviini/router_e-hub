namespace EmitterHub.DMX;

/// <summary>
/// Représente une trame DMX512 (512 canaux)
/// </summary>
public class DmxFrame
{
    public const int DMX_CHANNELS = 512;

    private readonly byte[] _channels;
    private int _activeChannelsCount = 0;
    public bool IsModified { get; private set; } = false;

    public byte[] Channels => _channels;
    public int Universe { get; set; }
    public string TargetIP { get; set; } = string.Empty;

    public DmxFrame(int universe = 0)
    {
        _channels = new byte[DMX_CHANNELS];
        Universe = universe;
    }

    public void SetChannel(int channel, byte value)
    {
        if (channel >= 1 && channel <= DMX_CHANNELS)
        {
            int index = channel - 1;
            if (_channels[index] == value) return; // Value hasn't changed

            bool wasActive = _channels[index] > 0;
            bool isActive = value > 0;

            _channels[index] = value;
            IsModified = true;

            if (wasActive && !isActive)
            {
                _activeChannelsCount--;
            }
            else if (!wasActive && isActive)
            {
                _activeChannelsCount++;
            }
        }
    }

    public byte GetChannel(int channel)
    {
        if (channel >= 1 && channel <= DMX_CHANNELS)
        {
            return _channels[channel - 1];
        }
        return 0;
    }

    public void SetRGB(int startChannel, byte r, byte g, byte b)
    {
        SetChannel(startChannel, r);
        SetChannel(startChannel + 1, g);
        SetChannel(startChannel + 2, b);
    }

    public void SetRGBW(int startChannel, byte r, byte g, byte b, byte w)
    {
        SetChannel(startChannel, r);
        SetChannel(startChannel + 1, g);
        SetChannel(startChannel + 2, b);
        SetChannel(startChannel + 3, w);
    }

    public void Clear()
    {
        bool needsModificationMark = _activeChannelsCount > 0;
        Array.Clear(_channels, 0, DMX_CHANNELS);
        _activeChannelsCount = 0;
        if (needsModificationMark)
        {
            IsModified = true;
        }
    }

    public bool HasData() => _activeChannelsCount > 0;

    public void MarkAsSent()
    {
        IsModified = false;
    }

    public void CopyTo(DmxFrame destination)
    {
        Array.Copy(_channels, destination._channels, DMX_CHANNELS);
        destination.Universe = Universe;
        destination.TargetIP = TargetIP;
    }

    public override string ToString()
    {
        int activeChannels = _channels.Count(c => c > 0);
        return $"DMX Universe {Universe} -> {TargetIP} ({activeChannels} active channels)";
    }
}
