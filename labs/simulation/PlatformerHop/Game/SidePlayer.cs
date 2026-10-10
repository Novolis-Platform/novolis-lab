using System.Numerics;

namespace PlatformerHop.Game;

internal sealed class SidePlayer
{
    private const float MoveSpeed = 5.5f;
    private const float JumpSpeed = 9f;
    private const float Gravity = 28f;

    public Vector3 Position { get; private set; }
    public float VelocityY { get; private set; }

    public void Reset(SideLevel level)
    {
        Position = new Vector3(1.5f, 2f, 0f);
        VelocityY = 0f;
        SnapToGround(level);
    }

    public void Update(SideLevel level, float moveAxis, bool jumpPressed, float dt)
    {
        var pos = Position;
        pos.X += moveAxis * MoveSpeed * dt;

        var grounded = IsGrounded(level, pos);
        if (grounded && jumpPressed)
        {
            VelocityY = JumpSpeed;
            grounded = false;
        }

        if (!grounded)
        {
            VelocityY -= Gravity * dt;
            pos.Y += VelocityY * dt;
        }

        pos = ResolveHorizontal(level, pos);
        var vy = VelocityY;
        pos = ResolveVertical(level, pos, ref vy);
        VelocityY = vy;
        Position = pos;
    }

    private static bool IsGrounded(SideLevel level, Vector3 pos)
    {
        var probe = pos + new Vector3(0f, -SideLevel.PlayerRadius - 0.02f, 0f);
        return level.IsSolidAtWorld(probe.X, probe.Y);
    }

    private static Vector3 ResolveHorizontal(SideLevel level, Vector3 pos)
    {
        var r = SideLevel.PlayerRadius;
        if (level.IsSolidAtWorld(pos.X - r, pos.Y) || level.IsSolidAtWorld(pos.X - r, pos.Y + r))
            pos.X = MathF.Ceiling((pos.X - r) / SideLevel.CellSize) * SideLevel.CellSize + r;
        if (level.IsSolidAtWorld(pos.X + r, pos.Y) || level.IsSolidAtWorld(pos.X + r, pos.Y + r))
            pos.X = MathF.Floor((pos.X + r) / SideLevel.CellSize) * SideLevel.CellSize - r;
        return pos;
    }

    private static Vector3 ResolveVertical(SideLevel level, Vector3 pos, ref float velocityY)
    {
        var r = SideLevel.PlayerRadius;
        if (velocityY <= 0f && level.IsSolidAtWorld(pos.X, pos.Y - r))
        {
            pos.Y = MathF.Ceiling((pos.Y - r) / SideLevel.CellSize) * SideLevel.CellSize + r;
            velocityY = 0f;
        }
        else if (velocityY > 0f && level.IsSolidAtWorld(pos.X, pos.Y + r))
        {
            pos.Y = MathF.Floor((pos.Y + r) / SideLevel.CellSize) * SideLevel.CellSize - r;
            velocityY = 0f;
        }

        return pos;
    }

    private void SnapToGround(SideLevel level)
    {
        var pos = Position;
        while (!IsGrounded(level, pos) && pos.Y > 0f)
            pos.Y -= SideLevel.CellSize * 0.25f;
        Position = pos;
        VelocityY = 0f;
    }
}
