using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO.Compression;

public class ListenEHub
{
    public static async Task Main(string[] args)
    {
        int port = 8765;
        int targetUniverse = 0;

        using var udp = new UdpClient(port);
        Console.WriteLine($"[eHuB] 🔌 En écoute sur UDP {port} (univers ciblé = {targetUniverse})...\n");

        while (true)
        {
            var result = await udp.ReceiveAsync();
            var buffer = result.Buffer;

            Console.WriteLine($"📩 Message reçu - Longueur : {buffer.Length} octets");

            if (buffer.Length < 6)
            {
                Console.WriteLine("⚠️ Message trop court, ignoré.\n");
                continue;
            }

            // Afficher le contenu brut du message en hexa
            Console.WriteLine("🔍 Contenu brut : " + BitConverter.ToString(buffer).Replace("-", " "));

            if (buffer[0] != 'e' || buffer[1] != 'H' || buffer[2] != 'u' || buffer[3] != 'B')
            {
                Console.WriteLine("❌ Signature 'eHuB' non trouvée, ignoré.\n");
                continue;
            }

            byte type = buffer[4];
            byte universe = buffer[5];

            Console.WriteLine($"🧾 Type = {type}, Univers = {universe}");

            if (universe != targetUniverse)
            {
                Console.WriteLine("⛔ Univers non concerné, ignoré.\n");
                continue;
            }

            switch (type)
            {
                case 1:
                    Console.WriteLine("🟠 Message de configuration reçu (non traité ici).\n");
                    break;

                case 2:
                    if (buffer.Length < 10)
                    {
                        Console.WriteLine("⚠️ Message update trop court.\n");
                        continue;
                    }

                    ushort entityCount = BitConverter.ToUInt16(buffer, 6);
                    ushort compressedSize = BitConverter.ToUInt16(buffer, 8);

                    Console.WriteLine($"🟢 Message d’update : {entityCount} entités, {compressedSize} octets compressés");

                    if (buffer.Length < 10 + compressedSize)
                    {
                        Console.WriteLine("❌ Taille incohérente, message ignoré.\n");
                        continue;
                    }

                    byte[] compressed = new byte[compressedSize];
                    Array.Copy(buffer, 10, compressed, 0, compressedSize);

                    byte[] decompressed = DecompressGzip(compressed);

                    for (int i = 0; i < entityCount; i++)
                    {
                        int offset = i * 6;
                        if (offset + 6 > decompressed.Length) break;

                        ushort id = BitConverter.ToUInt16(decompressed, offset);
                        byte r = decompressed[offset + 2];
                        byte g = decompressed[offset + 3];
                        byte b = decompressed[offset + 4];
                        byte w = decompressed[offset + 5];

                        Console.WriteLine($"🔸 Entity {id:0000} : R={r} G={g} B={b} W={w}");
                    }

                    Console.WriteLine(); // Ligne vide
                    break;

                default:
                    Console.WriteLine($"🔴 Type inconnu ({type}) reçu, message ignoré.\n");
                    break;
            }
        }
    }

    private static byte[] DecompressGzip(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
