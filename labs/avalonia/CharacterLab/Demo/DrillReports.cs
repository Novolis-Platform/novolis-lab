using System.Numerics;

namespace CharacterLab.Demo;

internal readonly record struct SkinStatsReport(
    string Source,
    int Vertices,
    int Triangles,
    int BonesWithPrimary,
    int MultiInfluenceVerts,
    float HeightMeters);
