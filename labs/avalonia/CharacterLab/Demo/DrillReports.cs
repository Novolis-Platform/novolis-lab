using System.Numerics;

namespace CharacterLab.Demo;

internal readonly record struct SkinStatsReport(
    string Source,
    int Vertices,
    int Triangles,
    int BonesWithPrimary,
    int MultiInfluenceVerts,
    float HeightMeters);

internal readonly record struct PoseSampleReport(
    string Phase,
    float Time,
    Vector3 Hips,
    Vector3 Head,
    Vector3 LeftHand,
    Vector3 RightHand,
    Vector3 LeftFoot,
    Vector3 RightFoot,
    Vector3 RifleButt,
    Vector3 RifleTip);

internal readonly record struct VertexDeltaReport(
    string PhaseA,
    string PhaseB,
    float TimeA,
    float TimeB,
    float MaxDelta,
    float MeanDelta,
    float UpperBodyMaxDelta,
    float LowerBodyMeanDelta,
    float BindHeadY);

internal readonly record struct BoneTravelReport(
    string PhaseA,
    string PhaseB,
    float TimeA,
    float TimeB,
    float Head,
    float RightHand,
    float LeftHand,
    float RightFoot,
    float LeftFoot,
    float Hips,
    float Spine2);

internal readonly record struct HoldLockReport(
    string Phase,
    float Time,
    Vector3 PrimaryHold,
    Vector3 SecondaryHold,
    Vector3 RightHand,
    Vector3 LeftHand,
    float RightHandError,
    float LeftHandError);
