using System.Numerics;

namespace KatoriLab.Demo;

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
