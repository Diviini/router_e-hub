using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
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

            var lines = File.ReadAllLines(path);
            if (lines.Length <= 1)
            {
                Console.WriteLine("❌ Fichier CSV vide ou uniquement l'en-tête.");
                return;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(';');

                if (cols.Length < 7)
                {
                    Console.WriteLine($"⚠️ Ligne {i + 1} ignorée (colonnes insuffisantes)");
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

                    // Ajout au routeur
                    router.AddEntityRange(
                        entityStart,
                        entityEnd,
                        ip,
                        universeStart,
                        universeEnd,
                        channelMode,
                        dmxStartChannel
                    );

                    Console.WriteLine($"✅ Ligne {i + 1} : {entityStart}-{entityEnd} → {ip} [U{universeStart}-{universeEnd}]");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erreur ligne {i + 1} : {ex.Message}");
                }
            }

            Console.WriteLine("✅ Mapping terminé.");
        }
    }
}
