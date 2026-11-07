using System.Numerics;

namespace ArtillerySimulator;

/// <summary>
/// Live defense scenario driven by a <see cref="IGameLevel"/>.
/// New maps implement this (or reuse a shared mission engine with different spawn/objective data).
/// </summary>
internal interface IDefenseMission
{
    float GunX { get; }
    float GunZ { get; }
    Vector3 Objective { get; }
    float TrailBearingDegrees { get; }
    float OpeningElevationDegrees { get; }
    int OpeningChargeIndex { get; }
    float OpeningAzimuthDegrees { get; }

    bool ObjectiveLost { get; }
    bool FratricideLoss { get; }
    bool Held { get; }
    int Round { get; }
    int Remaining { get; }
    int Stopped { get; }
    int DefendersAlive { get; }
    float NearestMeters { get; }
    int LastBracket { get; }
    int LastPinned { get; }
    int SpawnPointCount { get; }
    string AvenueName { get; }

    float CaughtMeters { get; }
    float PinnedMeters { get; }

    void Advance(float simSeconds, Vector3? incomingShell);
    void ResolveImpact(Vector3 impact);
    void Draw(SilkWireBatch wires);

    string HudLine();
    string Observation();
    string ResultPhrase();
    string PostureToken();

    Vector3 NearestAlive(Vector3 gun);
    Vector3 BattleFocus(Vector3 gun);

    ThreatReport[] Reports(Vector3 gun);
    DefenderReport[] DefenderReports();
}
