namespace EmitterHub.DMX;

/// <summary>
/// Représente une trame DMX512 (512 canaux)
/// </summary>
public class DmxFrame
{
    public const int DMX_CHANNELS = 512;

    private readonly byte[] _channels;

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
            _channels[channel - 1] = value;
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
        Array.Clear(_channels, 0, DMX_CHANNELS);
    }

    public bool HasData()
    {
        return _channels.Any(c => c > 0);
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
