using System.Numerics;

namespace TopDownDoom.Game;

/// <summary>Doom-style movement: high speed, short accel, high friction, no stamina.</summary>
internal static class DoomMovement
{
    public const float MaxSpeed = 9.5f;
    public const float Acceleration = 52f;
    public const float Friction = 14f;

    public static Vector2 Integrate(Vector2 velocity, Vector2 wishDir, float dt)
    {
        if (wishDir.LengthSquared() > 1e-6f)
        {
            wishDir = Vector2.Normalize(wishDir);
            velocity += wishDir * (Acceleration * dt);
            if (velocity.Length() > MaxSpeed)
            {
                velocity = Vector2.Normalize(velocity) * MaxSpeed;
            }
        }
        else
        {
            var speed = velocity.Length();
            if (speed > 0f)
            {
                var drop = speed * Friction * dt;
                var newSpeed = MathF.Max(0f, speed - drop);
                velocity = speed > 1e-6f ? velocity * (newSpeed / speed) : Vector2.Zero;
            }
        }

        return velocity;
    }
}
