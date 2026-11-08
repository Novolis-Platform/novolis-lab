namespace CalypsoCad.Model;

/// <summary>Engineering capabilities — orthogonal to <see cref="CadSystem"/>.</summary>
[Flags]
internal enum PartFunction
{
    None = 0,
    LoadBearing = 1,
    PressureTight = 2,
    HullAttached = 4,
    Replaceable = 8,
}
