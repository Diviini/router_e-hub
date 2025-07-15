using System;
using System.IO;
using System.Linq;
using EmitterHub.Routing;

namespace EmitterHub.eHub
{
    public static class CsvMappingLoader
    {
        public static void Load(string path, Router router)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"❌ Fichier CSV introuvable : {path}");
                return;
            }

            Console.WriteLine($"📥 Chargement du mapping depuis : {path}");

            var lines = File.ReadLines(path).Skip(1); // On saute l'en-tête
            int lineCount = 1;

            foreach (var line in lines)
            {
                lineCount++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(';');

                if (cols.Length < 7)
                {
                    Console.WriteLine($"⚠️ Ligne {lineCount} ignorée (colonnes insuffisantes)");
                    continue;
                }

                try
                {
                    ushort entityStart = ushort.Parse(cols[0]);
                    ushort entityEnd = ushort.Parse(cols[1]);
                    string ip = cols[2];
                    ushort universeStart = ushort.Parse(cols[3]);
                    ushort universeEnd = ushort.Parse(cols[4]);
                    string channelMode = cols[5].Trim().ToUpper();
                    ushort dmxStartChannel = ushort.Parse(cols[6]);

                    router.AddEntityRange(
                        entityStart,
                        entityEnd,
                        ip,
                        universeStart,
                        universeEnd,
                        channelMode,
                        dmxStartChannel
                    );

                    Console.WriteLine($"✅ Ligne {lineCount} : {entityStart}-{entityEnd} → {ip} [U{universeStart}-{universeEnd}]");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erreur ligne {lineCount} : {ex.Message}");
                }
            }

            Console.WriteLine("✅ Mapping terminé.");
        }
    }
}
