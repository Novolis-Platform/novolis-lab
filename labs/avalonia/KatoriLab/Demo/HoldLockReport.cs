using System.Numerics;

namespace KatoriLab.Demo;

internal readonly record struct HoldLockReport(
    string Phase,
    float Time,
    Vector3 PrimaryHold,
    Vector3 SecondaryHold,
    Vector3 RightHand,
    Vector3 LeftHand,
    float RightHandError,
    float LeftHandError);
