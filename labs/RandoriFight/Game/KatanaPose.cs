using System.Numerics;

namespace RandoriFight.Game;

/// <summary>Local-space body and katana geometry (facing +X before mirror).</summary>
internal readonly struct KatanaPose(
    Vector3 leftFoot,
    Vector3 rightFoot,
    Vector3 hips,
    Vector3 chest,
    Vector3 head,
    Vector3 leftHand,
    Vector3 rightHand,
    Vector3 bladeRoot,
    Vector3 bladeTip)
{
    public Vector3 LeftFoot { get; } = leftFoot;
    public Vector3 RightFoot { get; } = rightFoot;
    public Vector3 Hips { get; } = hips;
    public Vector3 Chest { get; } = chest;
    public Vector3 Head { get; } = head;
    public Vector3 LeftHand { get; } = leftHand;
    public Vector3 RightHand { get; } = rightHand;
    public Vector3 BladeRoot { get; } = bladeRoot;
    public Vector3 BladeTip { get; } = bladeTip;
}
