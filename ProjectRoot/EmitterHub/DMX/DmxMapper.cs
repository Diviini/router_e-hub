using EmitterHub.eHub;

namespace EmitterHub.DMX;

public class EntityRange
{
    public ushort Start { get; set; }
    public ushort End { get; set; }
}

/// <summary>
/// Mappe les entités vers les trames DMX selon la configuration
/// </summary>
public class DmxMapper
{
    private readonly Dictionary<int, DmxFrame> _frames;
    private readonly Dictionary<ushort, EntityMapping> _entityMappings;

    private readonly List<EntityRange> _declaredRanges = new();

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
        ushort entityStart, ushort entityEnd, string ip,
        ushort universeStart, ushort universeEnd,
        string channelMode, ushort dmxStartChannel)
    {
        _declaredRanges.Add(new EntityRange { Start = entityStart, End = entityEnd });

        int stride = channelMode.ToUpperInvariant() switch
        {
            "RGBW" => 4,
            "RGB" => 3,
            _ => 3
        };

        ushort currentEntity = entityStart;
        ushort currentUniverse = universeStart;
        int currentChannel = Math.Clamp((int)dmxStartChannel, 1, 512);

        while (currentEntity <= entityEnd && currentUniverse <= universeEnd)
        {
            AddEntityMapping(currentEntity, ip, currentUniverse, currentChannel);

            currentEntity++;
            currentChannel += stride;

            if (currentChannel + (stride - 1) > DmxFrame.DMX_CHANNELS)
            {
                currentUniverse++;
                currentChannel = 1;
            }
        }
    }

    public IReadOnlyList<EntityRange> GetDeclaredRanges() => _declaredRanges;

    /// <summary>
    /// Met à jour les trames DMX avec les nouvelles données d'entités
    /// </summary>
    public void UpdateEntities(Dictionary<ushort, EntityState> entities)
    {
        // Effacer toutes les trames
        foreach (var frame in _frames.Values)
        {
            frame.Clear();
        }

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
    /// Obtient toutes les trames DMX qui contiennent des données
    /// </summary>
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

    public void ApplyUpdatesIncremental(Dictionary<ushort, EntityState> updated)
    {
        foreach (var entity in updated.Values)
        {
            if (_entityMappings.TryGetValue(entity.Id, out var map) &&
                _frames.TryGetValue(map.Universe, out var frame))
            {
                frame.SetRGB(map.DmxChannel, entity.R, entity.G, entity.B);
            }
        }
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