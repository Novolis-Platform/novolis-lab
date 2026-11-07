namespace ArtillerySimulator.Game;

internal readonly struct ThreatReport
{
    public int Id { get; init; }

    public string Road { get; init; }

    public float X { get; init; }

    public float Z { get; init; }

    public bool Alive { get; init; }

    public bool InArc { get; init; }

    public float MetersFromGun { get; init; }

    public float BearingDegrees { get; init; }

    public float MetersToCrossing { get; init; }

    public string Posture { get; init; }
}
