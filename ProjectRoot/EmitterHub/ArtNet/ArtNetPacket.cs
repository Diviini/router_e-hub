using EmitterHub.DMX;

namespace EmitterHub.ArtNet;

/// <summary>
/// Représente un paquet ArtNet pour transporter des données DMX512
/// </summary>
public class ArtNetPacket
{
    public const int ARTNET_PORT = 6454;
    public const string ARTNET_HEADER = "Art-Net\0";
    public const ushort ARTNET_DMX_OPCODE = 0x5000;
    public const int ARTNET_HEADER_SIZE = 18;

    private readonly byte[] _packetData;

    public byte[] PacketData => _packetData;
    public int PacketSize => ARTNET_HEADER_SIZE + DmxFrame.DMX_CHANNELS;

    public ArtNetPacket(DmxFrame dmxFrame)
    {
        _packetData = new byte[PacketSize];
        BuildPacket(dmxFrame);
    }

    /// <summary>
    /// Construit le paquet ArtNet à partir d'une trame DMX
    /// </summary>
    private void BuildPacket(DmxFrame dmxFrame)
    {
        int offset = 0;

        // Header "Art-Net\0" (8 bytes)
        var headerBytes = System.Text.Encoding.ASCII.GetBytes(ARTNET_HEADER);
        Array.Copy(headerBytes, 0, _packetData, offset, headerBytes.Length);
        offset += 8;

        // OpCode (2 bytes) - 0x5000 pour ArtDMX (little endian)
        _packetData[offset++] = 0x00;
        _packetData[offset++] = 0x50;

        // Protocol Version (2 bytes) - toujours 14 (big endian)
        _packetData[offset++] = 0x00;
        _packetData[offset++] = 0x0E;

        // Sequence (1 byte) - 0 pour pas de séquencement
        _packetData[offset++] = 0x00;

        // Physical (1 byte) - port physique, 0 par défaut
        _packetData[offset++] = 0x00;

        // Universe (2 bytes) - little endian
        ushort universe = (ushort)dmxFrame.Universe;
        _packetData[offset++] = (byte)(universe & 0xFF);
        _packetData[offset++] = (byte)((universe >> 8) & 0xFF);

        // Length (2 bytes) - longueur des données DMX (big endian)
        _packetData[offset++] = (byte)((DmxFrame.DMX_CHANNELS >> 8) & 0xFF);
        _packetData[offset++] = (byte)(DmxFrame.DMX_CHANNELS & 0xFF);

        // Data (512 bytes) - données DMX
        Array.Copy(dmxFrame.Channels, 0, _packetData, offset, DmxFrame.DMX_CHANNELS);
    }

    /// <summary>
    /// Parse un paquet ArtNet reçu (pour monitoring)
    /// </summary>
    public static ArtNetInfo? ParsePacket(byte[] packetData)
    {
        if (packetData.Length < ARTNET_HEADER_SIZE)
            return null;

        // Vérifier le header
        var headerBytes = new byte[8];
        Array.Copy(packetData, 0, headerBytes, 0, 8);
        string header = System.Text.Encoding.ASCII.GetString(headerBytes);

        if (header != ARTNET_HEADER)
            return null;

        // Lire l'OpCode
        ushort opCode = (ushort)(packetData[8] | (packetData[9] << 8));

        if (opCode != ARTNET_DMX_OPCODE)
            return null; // Pas un paquet ArtDMX

        // Lire l'univers
        ushort universe = (ushort)(packetData[14] | (packetData[15] << 8));

        // Lire la longueur des données
        ushort dataLength = (ushort)((packetData[16] << 8) | packetData[17]);

        // Extraire les données DMX
        var dmxData = new byte[dataLength];
        if (packetData.Length >= ARTNET_HEADER_SIZE + dataLength)
        {
            Array.Copy(packetData, ARTNET_HEADER_SIZE, dmxData, 0, dataLength);
        }

        return new ArtNetInfo
        {
            Universe = universe,
            DataLength = dataLength,
            DmxData = dmxData,
            ActiveChannels = dmxData.Count(b => b > 0)
        };
    }
}

/// <summary>
/// Informations extraites d'un paquet ArtNet
/// </summary>
public class ArtNetInfo
{
    public ushort Universe { get; set; }
    public ushort DataLength { get; set; }
    public byte[] DmxData { get; set; } = Array.Empty<byte>();
    public int ActiveChannels { get; set; }

    public override string ToString()
    {
        return $"ArtNet Universe {Universe}: {ActiveChannels}/{DataLength} channels actifs";
    }
}