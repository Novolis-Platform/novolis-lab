using System.Numerics;

namespace ArtillerySimulator;

internal readonly record struct OslofjordenMissionBoot(
    Vector3 Objective,
    float GunX,
    float GunZ,
    float TrailBearingDegrees,
    GunSite Site);
