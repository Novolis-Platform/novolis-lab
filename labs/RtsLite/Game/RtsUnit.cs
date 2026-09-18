using System.Numerics;

namespace RtsLite.Game;

internal enum UnitTeam
{
    Player,
    Enemy,
}

internal sealed class RtsUnit
{
    public const float Radius = 0.28f;
    public const float MoveSpeed = 3.2f;

    public UnitTeam Team { get; init; }
    public Vector3 Position;
    public Vector3? MoveTarget;
    public bool Selected;

    public bool HasOrder => MoveTarget is not null;

    public void Tick(RtsArena arena, float dt)
    {
        if (MoveTarget is not { } target)
            return;

        var delta = target - Position;
        delta.Y = 0f;
        var dist = delta.Length();
        if (dist < 0.08f)
        {
            MoveTarget = null;
            return;
        }

        var step = Vector3.Normalize(delta) * (MoveSpeed * dt);
        if (step.Length() > dist)
            step = delta;

        var next = Position + step;
        if (!arena.IsBlocked(next.X, next.Z))
            Position = next;
        else
            MoveTarget = null;
    }
}
