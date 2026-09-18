using System.Numerics;
using Novolis.Simulation.View;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace RandoriFight.Game;

internal sealed class SideFightCamera
{
    private float _focusX;

    public RayCamera Build(int screenWidth, int screenHeight, Fighter player, Fighter opponent)
    {
        var targetX = (player.PositionX + opponent.PositionX) * 0.5f;
        var t = 0.14f;
        _focusX = float.Lerp(_focusX, targetX, t);

        var eye = new Vector3(_focusX, 2.1f, 11.5f);
        var target = new Vector3(_focusX, 1.05f, 0f);
        var pose = new ViewPose(eye, target, Vector3.UnitY, 42f);
        _ = screenWidth;
        _ = screenHeight;
        return RayCamera.Perspective(pose.Position, pose.Target, pose.Up, pose.FieldOfViewDegrees);
    }

    public void Snap(float focusX) => _focusX = focusX;
}
