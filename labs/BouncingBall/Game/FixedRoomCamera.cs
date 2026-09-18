using System.Numerics;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace BouncingBall.Game;

/// <summary>Fixed overview of the room; does not track the ball.</summary>
internal sealed class FixedRoomCamera
{
    private readonly Vector3 _eye;
    private readonly Vector3 _target;

    public FixedRoomCamera(Vector3 roomCenter)
    {
        _target = roomCenter;
        _eye = roomCenter + new Vector3(11f, 8f, 11f);
    }

    public RayCamera BuildRaylibCamera() =>
        RayCamera.Perspective(_eye, _target, Vector3.UnitY, 55f);
}
