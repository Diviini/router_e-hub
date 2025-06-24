namespace EmitterHub.eHub;

/// <summary>
/// Représente l'état d'une entité LED (RGBW)
/// </summary>
public struct EntityState
{
    public ushort Id { get; set; }
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte W { get; set; }

    public EntityState(ushort id, byte r, byte g, byte b, byte w = 0)
    {
        Id = id;
        R = r;
        G = g;
        B = b;
        W = w;
    }

    /// <summary>
    /// Vérifie si l'entité est éteinte (toutes les couleurs à 0)
    /// </summary>
    public bool IsOff => R == 0 && G == 0 && B == 0 && W == 0;

    /// <summary>
    /// Crée une entité éteinte
    /// </summary>
    public static EntityState CreateOff(ushort id) => new(id, 0, 0, 0, 0);

    public override string ToString()
    {
        return $"Entity {Id}: R={R}, G={G}, B={B}, W={W}";
    }
}