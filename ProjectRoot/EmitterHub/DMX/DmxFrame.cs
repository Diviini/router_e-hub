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

    /// <summary>
    /// Définit la valeur d'un canal (1-512)
    /// </summary>
    public void SetChannel(int channel, byte value)
    {
        if (channel >= 1 && channel <= DMX_CHANNELS)
        {
            _channels[channel - 1] = value;
        }
    }

    /// <summary>
    /// Obtient la valeur d'un canal (1-512)
    /// </summary>
    public byte GetChannel(int channel)
    {
        if (channel >= 1 && channel <= DMX_CHANNELS)
        {
            return _channels[channel - 1];
        }
        return 0;
    }

    /// <summary>
    /// Définit une couleur RGB sur 3 canaux consécutifs
    /// </summary>
    public void SetRGB(int startChannel, byte r, byte g, byte b)
    {
        SetChannel(startChannel, r);
        SetChannel(startChannel + 1, g);
        SetChannel(startChannel + 2, b);
    }

    /// <summary>
    /// Définit une couleur RGBW sur 4 canaux consécutifs
    /// </summary>
    public void SetRGBW(int startChannel, byte r, byte g, byte b, byte w)
    {
        SetChannel(startChannel, r);
        SetChannel(startChannel + 1, g);
        SetChannel(startChannel + 2, b);
        SetChannel(startChannel + 3, w);
    }

    /// <summary>
    /// Remet tous les canaux à zéro
    /// </summary>
    public void Clear()
    {
        Array.Clear(_channels, 0, DMX_CHANNELS);
    }

    /// <summary>
    /// Vérifie si la trame contient des données (au moins un canal non-zéro)
    /// </summary>
    public bool HasData()
    {
        return _channels.Any(c => c > 0);
    }

    /// <summary>
    /// Copie les données vers un autre DmxFrame
    /// </summary>
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