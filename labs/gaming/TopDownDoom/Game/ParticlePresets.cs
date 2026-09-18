using System.Numerics;
using Novolis.Math.Geometry;
using TopDownDoom.Design;

namespace TopDownDoom.Game;

internal static class ParticlePresets
{
    public static void MuzzleFlash(ParticleSystem ps, Vector3 origin, float aimRadians, WeaponSpec weapon)
    {
        var dir = new Vector2(MathF.Cos(aimRadians), MathF.Sin(aimRadians));
        var mouth = origin + new Vector3(dir.X * 0.45f, 0f, dir.Y * 0.45f);
        var count = weapon.PelletCount > 1 ? 18 : 10;
        for (var i = 0; i < count; i++)
        {
            var spread = Random.Shared.NextSingle() * MathF.PI * 2f;
            var speed = 2.5f + Random.Shared.NextSingle() * 4f;
            var vel = new Vector3(MathF.Cos(spread) * speed, 0f, MathF.Sin(spread) * speed);
            ps.Emit(new Particle
            {
                Position = mouth,
                Velocity = vel + new Vector3(dir.X * 3f, 0f, dir.Y * 3f),
                MaxLife = 0.12f + Random.Shared.NextSingle() * 0.1f,
                Life = 0.22f,
                ColorStart = new Rgba32(255, 250, 200, 255),
                ColorEnd = new Rgba32(255, 120, 40, 0),
                SizeStart = 0.22f,
                SizeEnd = 0.05f,
                Sprite = ParticleSprite.Spark,
                Drag = 8f,
            });
        }

        for (var i = 0; i < 6; i++)
        {
            ps.Emit(new Particle
            {
                Position = mouth,
                Velocity = new Vector3(dir.X * (1f + i * 0.3f), 0f, dir.Y * (1f + i * 0.3f)),
                MaxLife = 0.25f,
                Life = 0.3f,
                ColorStart = new Rgba32(200, 200, 210, 120),
                ColorEnd = new Rgba32(80, 80, 90, 0),
                SizeStart = 0.35f,
                SizeEnd = 0.7f,
                Sprite = ParticleSprite.Smoke,
                Drag = 2f,
            });
        }

        if (weapon.PelletCount <= 2)
        {
            EjectShell(ps, mouth, aimRadians);
        }
    }

    public static void EjectShell(ParticleSystem ps, Vector3 origin, float aimRadians)
    {
        var side = aimRadians + MathF.PI * 0.5f;
        ps.Emit(new Particle
        {
            Position = origin,
            Velocity = new Vector3(MathF.Cos(side) * 2.2f, 0f, MathF.Sin(side) * 2.2f),
            MaxLife = 0.5f,
            Life = 0.55f,
            ColorStart = new Rgba32(220, 190, 80, 255),
            ColorEnd = new Rgba32(140, 110, 50, 180),
            SizeStart = 0.1f,
            SizeEnd = 0.08f,
            Sprite = ParticleSprite.Shell,
            Drag = 1.5f,
            Spin = 14f,
        });
    }

    public static void BulletTrail(ParticleSystem ps, Vector3 pos, bool fromPlayer, bool rocket)
    {
        ps.Emit(new Particle
        {
            Position = pos,
            Velocity = Vector3.Zero,
            MaxLife = rocket ? 0.35f : 0.14f,
            Life = rocket ? 0.4f : 0.16f,
            ColorStart = fromPlayer
                ? new Rgba32(255, 240, 140, 220)
                : new Rgba32(255, 90, 90, 200),
            ColorEnd = new Rgba32(255, 80, 40, 0),
            SizeStart = rocket ? 0.2f : 0.1f,
            SizeEnd = rocket ? 0.35f : 0.02f,
            Sprite = ParticleSprite.SoftGlow,
            Drag = 0f,
        });
    }

    public static void HitSparks(ParticleSystem ps, Vector3 pos, Rgba32 color, int count = 14)
    {
        for (var i = 0; i < count; i++)
        {
            var a = Random.Shared.NextSingle() * MathF.PI * 2f;
            var speed = 2f + Random.Shared.NextSingle() * 5f;
            ps.Emit(new Particle
            {
                Position = pos,
                Velocity = new Vector3(MathF.Cos(a) * speed, 0f, MathF.Sin(a) * speed),
                MaxLife = 0.15f + Random.Shared.NextSingle() * 0.2f,
                Life = 0.25f,
                ColorStart = color,
                ColorEnd = new Rgba32(color.R, color.G, color.B, 0),
                SizeStart = 0.14f,
                SizeEnd = 0.02f,
                Sprite = ParticleSprite.Spark,
                Drag = 6f,
            });
        }
    }

    public static void BloodSpray(ParticleSystem ps, Vector3 pos, float scale = 1f)
    {
        for (var i = 0; i < (int)(22 * scale); i++)
        {
            var a = Random.Shared.NextSingle() * MathF.PI * 2f;
            var speed = 1.5f + Random.Shared.NextSingle() * 4.5f * scale;
            ps.Emit(new Particle
            {
                Position = pos,
                Velocity = new Vector3(MathF.Cos(a) * speed, 0f, MathF.Sin(a) * speed),
                MaxLife = 0.35f + Random.Shared.NextSingle() * 0.4f,
                Life = 0.5f,
                ColorStart = new Rgba32(200, 20, 20, 230),
                ColorEnd = new Rgba32(80, 10, 10, 0),
                SizeStart = 0.12f * scale,
                SizeEnd = 0.25f * scale,
                Sprite = ParticleSprite.SoftGlow,
                Drag = 3f,
            });
        }
    }

    public static void Explosion(ParticleSystem ps, Vector3 pos, float scale = 1f)
    {
        for (var ring = 0; ring < 3; ring++)
        {
            var n = (int)(16 + ring * 10);
            for (var i = 0; i < n; i++)
            {
                var a = i / (float)n * MathF.PI * 2f;
                var speed = (3f + ring * 2.5f) * scale;
                ps.Emit(new Particle
                {
                    Position = pos,
                    Velocity = new Vector3(MathF.Cos(a) * speed, 0f, MathF.Sin(a) * speed),
                    MaxLife = 0.35f + ring * 0.15f,
                    Life = 0.45f,
                    ColorStart = ring == 0
                        ? new Rgba32(255, 250, 200, 255)
                        : new Rgba32(255, (byte)(100 + ring * 30), 30, 220),
                    ColorEnd = new Rgba32(60, 20, 10, 0),
                    SizeStart = (0.25f + ring * 0.12f) * scale,
                    SizeEnd = (0.5f + ring * 0.2f) * scale,
                    Sprite = ring == 0 ? ParticleSprite.Spark : ParticleSprite.SoftGlow,
                    Drag = 2.5f,
                });
            }
        }

        for (var i = 0; i < (int)(12 * scale); i++)
        {
            ps.Emit(new Particle
            {
                Position = pos + new Vector3(Random.Shared.NextSingle() - 0.5f, 0f, Random.Shared.NextSingle() - 0.5f),
                Velocity = new Vector3(
                    (Random.Shared.NextSingle() - 0.5f) * 2f,
                    0f,
                    (Random.Shared.NextSingle() - 0.5f) * 2f),
                MaxLife = 0.6f,
                Life = 0.7f,
                ColorStart = new Rgba32(90, 85, 95, 160),
                ColorEnd = new Rgba32(40, 38, 45, 0),
                SizeStart = 0.4f * scale,
                SizeEnd = 1.1f * scale,
                Sprite = ParticleSprite.Smoke,
                Drag = 1.2f,
            });
        }
    }

    public static void EnemyShot(ParticleSystem ps, Vector3 pos, Vector2 dir)
    {
        ps.Emit(new Particle
        {
            Position = pos,
            Velocity = new Vector3(dir.X * 2f, 0f, dir.Y * 2f),
            MaxLife = 0.1f,
            Life = 0.12f,
            ColorStart = new Rgba32(255, 80, 120, 255),
            ColorEnd = new Rgba32(255, 40, 40, 0),
            SizeStart = 0.18f,
            SizeEnd = 0.04f,
            Sprite = ParticleSprite.Spark,
            Drag = 5f,
        });
        HitSparks(ps, pos, new Rgba32(255, 120, 80, 200), 4);
    }

    public static void HitscanBeam(ParticleSystem ps, Vector3 from, Vector3 to)
    {
        var steps = 8;
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var p = Vector3.Lerp(from, to, t);
            ps.Emit(new Particle
            {
                Position = p,
                Velocity = Vector3.Zero,
                MaxLife = 0.08f,
                Life = 0.1f,
                ColorStart = new Rgba32(255, 60, 60, 200),
                ColorEnd = new Rgba32(255, 200, 100, 0),
                SizeStart = 0.08f,
                SizeEnd = 0.14f,
                Sprite = ParticleSprite.Spark,
                Drag = 0f,
            });
        }
    }

    public static void DashDust(ParticleSystem ps, Vector3 pos, Vector2 moveDir)
    {
        if (moveDir.LengthSquared() < 0.01f)
        {
            return;
        }

        moveDir = Vector2.Normalize(moveDir);
        for (var i = 0; i < 5; i++)
        {
            ps.Emit(new Particle
            {
                Position = pos - new Vector3(moveDir.X * 0.3f, 0f, moveDir.Y * 0.3f),
                Velocity = new Vector3(
                    -moveDir.X * (2f + Random.Shared.NextSingle() * 2f) + (Random.Shared.NextSingle() - 0.5f),
                    0f,
                    -moveDir.Y * (2f + Random.Shared.NextSingle() * 2f) + (Random.Shared.NextSingle() - 0.5f)),
                MaxLife = 0.25f,
                Life = 0.3f,
                ColorStart = new Rgba32(140, 120, 100, 140),
                ColorEnd = new Rgba32(60, 50, 45, 0),
                SizeStart = 0.2f,
                SizeEnd = 0.45f,
                Sprite = ParticleSprite.Smoke,
                Drag = 4f,
            });
        }
    }

    public static void PickupBurst(ParticleSystem ps, Vector3 pos, PickupKind kind)
    {
        var color = kind switch
        {
            PickupKind.Health => new Rgba32(80, 255, 100, 255),
            PickupKind.Armor => new Rgba32(100, 160, 255, 255),
            PickupKind.Ammo => new Rgba32(255, 220, 80, 255),
            PickupKind.BlueKey => new Rgba32(100, 160, 255, 255),
            PickupKind.Exit => new Rgba32(240, 240, 255, 255),
            _ => new Rgba32(220, 220, 220, 255),
        };
        for (var i = 0; i < 24; i++)
        {
            var a = i / 24f * MathF.PI * 2f;
            ps.Emit(new Particle
            {
                Position = pos,
                Velocity = new Vector3(MathF.Cos(a) * 3.5f, 0f, MathF.Sin(a) * 3.5f),
                MaxLife = 0.4f,
                Life = 0.45f,
                ColorStart = color,
                ColorEnd = new Rgba32(color.R, color.G, color.B, 0),
                SizeStart = 0.15f,
                SizeEnd = 0.02f,
                Sprite = ParticleSprite.Spark,
                Drag = 4f,
            });
        }
    }

    public static void ClosetSpawn(ParticleSystem ps, Vector3 pos)
    {
        Explosion(ps, pos, 0.85f);
        for (var i = 0; i < 30; i++)
        {
            var a = Random.Shared.NextSingle() * MathF.PI * 2f;
            ps.Emit(new Particle
            {
                Position = pos,
                Velocity = new Vector3(MathF.Cos(a) * 5f, 0f, MathF.Sin(a) * 5f),
                MaxLife = 0.5f,
                Life = 0.55f,
                ColorStart = new Rgba32(120, 255, 140, 255),
                ColorEnd = new Rgba32(40, 120, 60, 0),
                SizeStart = 0.12f,
                SizeEnd = 0.3f,
                Sprite = ParticleSprite.Spark,
                Drag = 3f,
            });
        }
    }

    public static void AmbientEmber(ParticleSystem ps, Vector3 pos)
    {
        ps.Emit(new Particle
        {
            Position = pos,
            Velocity = new Vector3(
                (Random.Shared.NextSingle() - 0.5f) * 0.4f,
                0f,
                0.3f + Random.Shared.NextSingle() * 0.5f),
            MaxLife = 1.2f + Random.Shared.NextSingle(),
            Life = 1.5f,
            ColorStart = new Rgba32(255, 90, 40, 80),
            ColorEnd = new Rgba32(80, 30, 20, 0),
            SizeStart = 0.06f,
            SizeEnd = 0.14f,
            Sprite = ParticleSprite.SoftGlow,
            Drag = 0.5f,
        });
    }

    public static void BarrelFuse(ParticleSystem ps, Vector3 pos)
    {
        ps.Emit(new Particle
        {
            Position = pos + new Vector3(0f, 0f, 0.35f),
            Velocity = new Vector3(0f, 0f, 0.6f),
            MaxLife = 0.2f,
            Life = 0.22f,
            ColorStart = new Rgba32(255, 200, 60, 255),
            ColorEnd = new Rgba32(255, 80, 20, 0),
            SizeStart = 0.08f,
            SizeEnd = 0.02f,
            Sprite = ParticleSprite.Spark,
            Drag = 1f,
        });
    }
}
