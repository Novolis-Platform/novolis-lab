using System.Numerics;
using Novolis.Simulation.Kinematics;
using PlatformerHop.Game;

namespace PlatformerTwoD.Game;

/// <summary>Side-view hopper mapped to Simulation planar XZ (vertical axis = Z).</summary>
internal sealed class PlanarHopPlayer
{
    private const float MoveSpeed = 5.5f;
    private const float JumpSpeed = 9f;
    private const float Gravity = 28f;

    public Vector3 Position { get; private set; }
    public float VelocityZ { get; private set; }

    public void Reset(SideLevel level)
    {
        Position = new Vector3(1.5f, 0f, 2f);
        VelocityZ = 0f;
        SnapToGround(level);
    }

    public void Update(SideLevel level, float moveAxis, bool jumpPressed, float dt)
    {
        var pos = Position;
        var horizontal = new Vector3(moveAxis * MoveSpeed * dt, 0f, 0f);
        pos = PlanarAgent.Move(level.Tiles, pos, horizontal, SideLevel.PlayerRadius, SideLevel.CellSize);

        var grounded = IsGrounded(level, pos);
        if (grounded && jumpPressed)
        {
            VelocityZ = JumpSpeed;
            grounded = false;
        }

        if (!grounded)
        {
            VelocityZ -= Gravity * dt;
            var vertical = new Vector3(0f, 0f, VelocityZ * dt);
            pos = PlanarAgent.Move(level.Tiles, pos, vertical, SideLevel.PlayerRadius, SideLevel.CellSize);
        }
        else if (VelocityZ < 0f)
        {
            VelocityZ = 0f;
        }

        Position = pos;
    }

    private static bool IsGrounded(SideLevel level, Vector3 pos)
    {
        var probe = pos + new Vector3(0f, 0f, -SideLevel.PlayerRadius - 0.02f);
        return level.IsSolidAtWorld(probe.X, probe.Z);
    }

    private void SnapToGround(SideLevel level)
    {
        var pos = Position;
        while (!IsGrounded(level, pos) && pos.Z > 0f)
        {
            pos.Z -= SideLevel.CellSize * 0.25f;
        }

        Position = pos;
        VelocityZ = 0f;
    }
}
