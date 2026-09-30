using System.Numerics;

namespace KatoriLab.Demo;

internal readonly record struct PoseSampleReport(
    string Phase,
    float Time,
    Vector3 Hips,
    Vector3 Head,
    Vector3 LeftHand,
    Vector3 RightHand,
    Vector3 LeftFoot,
    Vector3 RightFoot,
    Vector3 Kashira,
    Vector3 Kissaki);
