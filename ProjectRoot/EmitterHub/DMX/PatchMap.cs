using System.Globalization;
using System.Text;

namespace EmitterHub.DMX
{
    /// <summary>
    /// Règle de patch : copie la valeur d'un canal (source) vers un ou plusieurs canaux (destinations).
    /// </summary>
    public sealed class PatchRule
    {
        public int SrcUniverse { get; set; }
        public int SrcChannel  { get; set; } // 1..512
        public int DstUniverse { get; set; }
        public int DstChannel  { get; set; } // 1..512

        public override string ToString()
            => $"U{SrcUniverse}:{SrcChannel} -> U{DstUniverse}:{DstChannel}";
    }

    /// <summary>
    /// Ensemble de règles + application sur un jeu de trames DMX.
    /// </summary>
    public sealed class PatchMap
    {
        private readonly List<PatchRule> _rules = new();

        public IReadOnlyList<PatchRule> Rules => _rules;

        public void Clear() => _rules.Clear();

        public void Add(PatchRule rule)
        {
            if (rule.SrcChannel < 1 || rule.SrcChannel > DmxFrame.DMX_CHANNELS
             || rule.DstChannel < 1 || rule.DstChannel > DmxFrame.DMX_CHANNELS)
                throw new ArgumentOutOfRangeException("Canaux DMX doivent être dans [1..512].");

            _rules.Add(rule);
        }

        /// <summary>
        /// Applique le patch : pour chaque règle, copie la valeur du (Usrc,Chsrc) vers (Udst,Chdst).
        /// Aucun écrasement des sources : on lit d’abord toutes les sources puis on écrit.
        /// </summary>
        public void Apply(IEnumerable<DmxFrame> frames)
        {
            if (_rules.Count == 0) return;

            // Indexer les frames par univers pour accès O(1)
            var byUniverse = frames.ToDictionary(f => f.Universe);

            // 1) Lire toutes les sources
            var values = new List<(int dstUni, int dstCh, byte val)>(_rules.Count);
            foreach (var r in _rules)
            {
                if (!byUniverse.TryGetValue(r.SrcUniverse, out var srcFrame))
                    continue;

                var v = srcFrame.GetChannel(r.SrcChannel);
                values.Add((r.DstUniverse, r.DstChannel, v));
            }

            // 2) Écrire les destinations
            foreach (var t in values)
            {
                if (!byUniverse.TryGetValue(t.dstUni, out var dstFrame))
                    continue;

                dstFrame.SetChannel(t.dstCh, t.val);
            }
        }

        public override string ToString()
            => $"{_rules.Count} règles de patch";
    }

    /// <summary>
    /// Chargement CSV simple : chaque ligne = "SrcUniverse;SrcChannel;DstUniverse;DstChannel"
    /// En-tête attendu (souple) : "SrcUniverse;SrcChannel;DstUniverse;DstChannel"
    /// </summary>
    public static class CsvPatchLoader
    {
        public static PatchMap Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Fichier introuvable : {path}");

            var lines = File.ReadAllLines(path);
            if (lines.Length == 0) throw new InvalidOperationException("CSV vide.");

            var map = new PatchMap();

            int startIdx = 0;
            // Skipper l'entête s'il semble présent
            if (IsHeader(lines[0])) startIdx = 1;

            for (int i = startIdx; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cols = line.Split(';');

                if (cols.Length < 4)
                    throw new FormatException($"Ligne {i+1}: 4 colonnes attendues (SrcUniverse;SrcChannel;DstUniverse;DstChannel).");

                try
                {
                    int su = int.Parse(cols[0], CultureInfo.InvariantCulture);
                    int sc = int.Parse(cols[1], CultureInfo.InvariantCulture);
                    int du = int.Parse(cols[2], CultureInfo.InvariantCulture);
                    int dc = int.Parse(cols[3], CultureInfo.InvariantCulture);

                    map.Add(new PatchRule
                    {
                        SrcUniverse = su,
                        SrcChannel  = sc,
                        DstUniverse = du,
                        DstChannel  = dc
                    });
                }
                catch (Exception ex)
                {
                    throw new FormatException($"Ligne {i+1}: {ex.Message}");
                }
            }

            return map;
        }

        private static bool IsHeader(string line)
        {
            var l = line.ToLowerInvariant();
            return l.Contains("srcuniverse") || l.Contains("srcchannel")
                || l.Contains("dstuniverse") || l.Contains("dstchannel");
        }
    }
}
