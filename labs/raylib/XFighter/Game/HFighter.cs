using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Rendering;

namespace XFighter.Game;

internal sealed class HFighter
{
    private const float OrbitInner = 32f;
    private const float OrbitOuter = 52f;
    private const float SeparationRadius = 14f;

    private static readonly Color BallHull = Color.FromArgb(255, 18, 18, 22);
    private static readonly Color SolarPanel = Color.FromArgb(255, 55, 58, 65);
    private static readonly Color PanelFrame = Color.FromArgb(255, 35, 38, 45);
    private static readonly Color EngineGlow = Color.FromArgb(255, 255, 50, 35);

    public Vector3 Position;
    public Vector3 Velocity;
    public float Health = 1f;
    public float WeavePhase;
    public float FireCooldown;
    public bool Active;

    public float HitRadius => 2.4f;

    public void Spawn(Vector3 playerPos, Vector3 playerForward, Random rng)
    {
        Active = true;
        Health = 1f;
        WeavePhase = (float)rng.NextDouble() * MathF.Tau;
        FireCooldown = 0.5f + (float)rng.NextDouble();

        var forward = Vector3.Normalize(playerForward);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var dist = 95f + (float)rng.NextDouble() * 55f;
        var lateral = ((float)rng.NextDouble() * 2f - 1f) * 48f;
        var vertical = (float)(rng.NextDouble() * 10 - 5);
        Position = playerPos + forward * dist + right * lateral + new Vector3(0, vertical, 0);
        Velocity = Vector3.Zero;
    }

    public void Update(float dt, Vector3 playerPos, ReadOnlySpan<HFighter> squadron)
    {
        if (!Active)
            return;

        FireCooldown = Math.Max(0, FireCooldown - dt);
        WeavePhase += dt * 1.8f;

        var toPlayer = playerPos - Position;
        var dist = toPlayer.Length();
        if (dist < 0.01f)
            return;

        var dir = toPlayer / dist;
        var right = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY));
        var separation = ComputeSeparation(squadron);
        var weave = right * MathF.Sin(WeavePhase) * 0.28f
            + new Vector3(0, MathF.Cos(WeavePhase * 0.6f) * 0.12f, 0);

        Vector3 desired;
        float speed;
        if (dist < OrbitInner)
        {
            desired = -dir * 1.2f + separation + weave;
            speed = 11f;
        }
        else if (dist > OrbitOuter)
        {
            desired = dir * 0.85f + separation + weave * 0.5f;
            speed = 10f;
        }
        else
        {
            var tangent = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY));
            desired = tangent * MathF.Sin(WeavePhase * 1.3f) + dir * 0.15f + separation + weave;
            speed = 12f;
        }

        if (desired.LengthSquared() < 1e-4f)
            desired = dir;

        Velocity = Vector3.Normalize(desired) * speed;
        Position += Velocity * dt;
    }

    public bool TryFire(Vector3 playerPos, int nearbyAllies)
    {
        if (!Active || FireCooldown > 0)
            return false;

        var toPlayer = playerPos - Position;
        var distSq = toPlayer.LengthSquared();
        if (distSq > 75f * 75f || distSq < 18f * 18f)
            return false;

        if (nearbyAllies >= 3 && Random.Shared.NextDouble() > 0.35)
            return false;

        FireCooldown = 1.1f + (float)Random.Shared.NextDouble() * 0.9f;
        return true;
    }

    public void GetBoltVelocity(Vector3 playerPos, out Vector3 origin, out Vector3 velocity)
    {
        var toPlayer = playerPos - Position;
        origin = Position + Vector3.Normalize(toPlayer) * 1.2f;
        velocity = Vector3.Normalize(toPlayer) * 88f;
    }

    private Vector3 ComputeSeparation(ReadOnlySpan<HFighter> squadron)
    {
        var push = Vector3.Zero;
        foreach (var other in squadron)
        {
            if (!other.Active || ReferenceEquals(other, this))
                continue;

            var offset = Position - other.Position;
            var len = offset.Length();
            if (len < SeparationRadius && len > 0.01f)
                push += offset / len * (SeparationRadius - len) * 0.55f;
        }

        return push;
    }

    public void Draw(RayGameContext ctx) => DrawInternal(ctx.DrawShipBox, ctx.DrawShipWires, ctx.DrawGlowSphere);

    public void DrawHarness() =>
        DrawInternal(World.DrawCubeV, World.DrawCubeWiresV, World.DrawSphere);

    private void DrawInternal(
        Action<Vector3, Vector3, Color> drawBox,
        Action<Vector3, Vector3, Color> drawWires,
        Action<Vector3, float, Color> drawSphere)
    {
        if (!Active)
            return;

        var forward = Vector3.Normalize(Velocity);
        if (forward.LengthSquared() < 1e-4f)
            forward = new Vector3(0, 0, 1);

        var right = Vector3.Normalize(Vector3.Cross(forward, new Vector3(0, 1, 0)));
        var center = Position;
        drawSphere(center, 1.05f, BallHull);
        drawBox(center + right * 3.6f, new Vector3(0.12f, 2.8f, 3.6f), SolarPanel);
        drawBox(center - right * 3.6f, new Vector3(0.12f, 2.8f, 3.6f), SolarPanel);
        drawBox(center + right * 3.6f, new Vector3(0.18f, 2.9f, 3.7f), PanelFrame);
        drawBox(center - right * 3.6f, new Vector3(0.18f, 2.9f, 3.7f), PanelFrame);
        drawSphere(center - forward * 0.9f, 0.28f, EngineGlow);
        drawWires(center, new Vector3(7.8f, 3.2f, 4.2f), Color.FromArgb(140, 25, 25, 30));
    }
}
