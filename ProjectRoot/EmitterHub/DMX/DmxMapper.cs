using EmitterHub.eHub;

namespace EmitterHub.DMX;

/// <summary>
/// Mappe les entités vers les trames DMX selon la configuration
/// </summary>
public class DmxMapper
{
    private readonly Dictionary<int, DmxFrame> _frames;
    private readonly Dictionary<ushort, EntityMapping> _entityMappings;

    public DmxMapper()
    {
        _frames = new Dictionary<int, DmxFrame>();
        _entityMappings = new Dictionary<ushort, EntityMapping>();
    }

    /// <summary>
    /// Ajoute un mapping d'entité vers DMX
    /// </summary>
    public void AddEntityMapping(ushort entityId, string targetIP, int universe, int dmxChannel)
    {
        _entityMappings[entityId] = new EntityMapping
        {
            EntityId = entityId,
            TargetIP = targetIP,
            Universe = universe,
            DmxChannel = dmxChannel
        };

        // Créer la trame DMX si elle n'existe pas
        if (!_frames.ContainsKey(universe))
        {
            _frames[universe] = new DmxFrame(universe) { TargetIP = targetIP };
        }
    }

    /// <summary>
    /// Ajoute un mapping pour une plage d'entités
    /// </summary>
    public void AddEntityRangeMapping(
    ushort entityStart,
    ushort entityEnd,
    string ip,
    ushort universeStart,
    ushort universeEnd,
    string channelMode,
    ushort dmxStartChannel)
    {

        int totalEntities = entityEnd - entityStart + 1;
        int universesCount = universeEnd - universeStart + 1;

        ushort currentEntity = entityStart;
        ushort currentUniverse = universeStart;
        ushort currentChannel = 1;

        for (int i = 0; i < totalEntities; i++)
        {
            AddEntityMapping(currentEntity, ip, currentUniverse, currentChannel);

            currentEntity++;
            currentChannel += 3; // RGB = 3 canaux

            // Passer à l'univers suivant si nécessaire
            if (currentChannel > 512 - 2) // Garder de la place pour les 3 canaux RGB
            {
                currentUniverse++;
                currentChannel = 1;
            }
        }
    }

    /// <summary>
    /// Met à jour les trames DMX avec les nouvelles données d'entités
    /// </summary>
    public void UpdateEntities(Dictionary<ushort, EntityState> entities)
    {
        // Mapper chaque entité
        foreach (var entity in entities.Values)
        {
            if (_entityMappings.TryGetValue(entity.Id, out var mapping))
            {
                if (_frames.TryGetValue(mapping.Universe, out var frame))
                {
                    // Mapper RGB (3 canaux consécutifs)
                    frame.SetRGB(mapping.DmxChannel, entity.R, entity.G, entity.B);
                }
            }
        }
    }

    /// <summary>
    /// Obtient toutes les trames DMX qui ont été modifiées depuis leur dernier envoi.
    /// </summary>
    public IEnumerable<DmxFrame> GetModifiedFrames()
    {
        return _frames.Values.Where(f => f.IsModified);
    }

    public IEnumerable<DmxFrame> GetActiveFrames()
    {
        return _frames.Values.Where(f => f.HasData());
    }


    /// <summary>
    /// Obtient toutes les trames DMX
    /// </summary>
    public IEnumerable<DmxFrame> GetAllFrames()
    {
        return _frames.Values;
    }

    /// <summary>
    /// Obtient les statistiques de mapping
    /// </summary>
    public MappingStats GetStats()
    {
        return new MappingStats
        {
            TotalEntities = _entityMappings.Count,
            TotalUniverses = _frames.Count,
            ActiveFrames = _frames.Values.Count(f => f.HasData())
        };
    }

    /// <summary>
    /// Efface tous les mappings
    /// </summary>
    public void Clear()
    {
        _entityMappings.Clear();
        _frames.Clear();
    }
}

/// <summary>
/// Représente le mapping d'une entité vers DMX
/// </summary>
public class EntityMapping
{
    public ushort EntityId { get; set; }
    public string TargetIP { get; set; } = string.Empty;
    public int Universe { get; set; }
    public int DmxChannel { get; set; }
}

/// <summary>
/// Statistiques de mapping
/// </summary>
public class MappingStats
{
    public int TotalEntities { get; set; }
    public int TotalUniverses { get; set; }
    public int ActiveFrames { get; set; }
}