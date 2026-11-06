namespace ArtillerySimulator.Game;

/// <summary>Sea (coastal, ~0 m) vs inland lakes vs land.</summary>
internal enum KristiansandSurfaceKind : byte
{
    Land = 0,
    Sea = 1,
    Lake = 2,
}
