using System.Numerics;
using FrankMoat.Weapons;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;

namespace FrankMoat.Particles;

internal static class ParticleEmit
{
    public static void Muzzle(SpriteParticleField field, Vector3 origin, Vector3 aim, WeaponSpec weapon)
    {
        var dir = aim.NormalizeXz();
        var mouth = origin + dir * 0.55f;
        var count = weapon.PelletCount > 1 ? 28 : weapon.Mode == WeaponFireMode.Rotary ? 16 : 12;
        for (var i = 0; i < count; i++)
        {
            var a = Random.Shared.NextSingle() * MathF.PI * 2f;
            var speed = 3f + Random.Shared.NextSingle() * 6f;
            field.Emit(new SpriteParticle
            {
                Position = mouth,
                Velocity = Vector3PlanarExtensions.FromHeadingXz(a) * speed + dir * 4f,
                Elevation = 0.55f,
                ElevationVelocity = 1.2f + Random.Shared.NextSingle(),
                MaxLife = 0.1f + Random.Shared.NextSingle() * 0.08f,
                ColorStart = new Rgba32(255, 244, 190, 255),
                ColorEnd = new Rgba32(255, 90, 20, 0),
                SizeStart = 0.2f,
                SizeEnd = 0.04f,
                Kind = (int)ParticleKind.Spark,
                Drag = 9f,
            });
        }

        var smoke = weapon.Mode == WeaponFireMode.Rotary ? 8 : 5;
        for (var i = 0; i < smoke; i++)
        {
            field.Emit(new SpriteParticle
            {
                Position = mouth,
                Velocity = dir * (0.8f + i * 0.15f),
                Elevation = 0.5f,
                ElevationVelocity = 0.4f,
                MaxLife = 0.35f + Random.Shared.NextSingle() * 0.2f,
                ColorStart = new Rgba32(70, 72, 78, 140),
                ColorEnd = new Rgba32(40, 42, 46, 0),
                SizeStart = 0.28f,
                SizeEnd = 0.7f,
                Kind = (int)ParticleKind.Smoke,
                Drag = 1.6f,
            });
        }

        Brass(field, mouth, dir);
    }

    public static void Brass(SpriteParticleField field, Vector3 origin, Vector3 aim)
    {
        var side = new Vector3(-aim.Z, 0f, aim.X);
        field.Emit(new SpriteParticle
        {
            Position = origin,
            Velocity = side * 2.4f + new Vector3((Random.Shared.NextSingle() - 0.5f), 0f, 0f),
            Elevation = 0.62f,
            ElevationVelocity = 3.4f + Random.Shared.NextSingle() * 1.4f,
            MaxLife = 1.1f,
            ColorStart = new Rgba32(220, 176, 64, 255),
            ColorEnd = new Rgba32(150, 110, 40, 200),
            SizeStart = 0.09f,
            SizeEnd = 0.08f,
            Kind = (int)ParticleKind.Shell,
            Drag = 1.4f,
            Spin = 16f,
        });
    }

    public static void Tracer(SpriteParticleField field, Vector3 position, bool fromFrank)
    {
        field.Emit(new SpriteParticle
        {
            Position = position,
            MaxLife = 0.09f,
            ColorStart = fromFrank ? new Rgba32(255, 230, 120, 220) : new Rgba32(255, 80, 70, 200),
            ColorEnd = new Rgba32(255, 80, 30, 0),
            SizeStart = 0.1f,
            SizeEnd = 0.03f,
            Kind = (int)ParticleKind.SoftGlow,
            Elevation = 0.45f,
        });
    }

    public static void Blood(SpriteParticleField field, Vector3 origin, Vector3 incoming, float scale)
    {
        var dir = incoming.NormalizeXz(new Vector3(0f, 0f, 1f));
        var n = (int)(26 * scale);
        for (var i = 0; i < n; i++)
        {
            var spread = (Random.Shared.NextSingle() - 0.5f) * 1.4f;
            var speed = 2f + Random.Shared.NextSingle() * 7f * scale;
            var vx = dir.X * speed + -dir.Z * spread * speed;
            var vz = dir.Z * speed + dir.X * spread * speed;
            field.Emit(new SpriteParticle
            {
                Position = origin,
                Velocity = new Vector3(vx, 0f, vz),
                Elevation = 0.4f,
                ElevationVelocity = 1.5f + Random.Shared.NextSingle() * 3f,
                MaxLife = 0.35f + Random.Shared.NextSingle() * 0.45f,
                ColorStart = new Rgba32(168, 14, 18, 240),
                ColorEnd = new Rgba32(70, 8, 10, 0),
                SizeStart = 0.1f * scale,
                SizeEnd = 0.22f * scale,
                Kind = (int)ParticleKind.Blood,
                Drag = 2.8f,
            });
        }
    }

    public static void WallImpact(SpriteParticleField field, Vector3 point, Vector3 normal, bool steel)
    {
        for (var i = 0; i < (steel ? 18 : 12); i++)
        {
            var a = Random.Shared.NextSingle() * MathF.PI * 2f;
            var speed = 2.5f + Random.Shared.NextSingle() * 6f;
            field.Emit(new SpriteParticle
            {
                Position = point,
                Velocity = normal.ToPlanar() * 3f + Vector3PlanarExtensions.FromHeadingXz(a) * speed,
                Elevation = 0.7f,
                ElevationVelocity = 2f + Random.Shared.NextSingle() * 2f,
                MaxLife = 0.16f + Random.Shared.NextSingle() * 0.14f,
                ColorStart = steel ? new Rgba32(255, 230, 160, 255) : new Rgba32(190, 176, 150, 230),
                ColorEnd = new Rgba32(80, 70, 50, 0),
                SizeStart = 0.12f,
                SizeEnd = 0.02f,
                Kind = (int)ParticleKind.Spark,
                Drag = 6f,
            });
        }

        for (var i = 0; i < 4; i++)
        {
            field.Emit(new SpriteParticle
            {
                Position = point,
                Velocity = normal.ToPlanar() * 0.4f,
                Elevation = 0.6f,
                MaxLife = 0.28f,
                ColorStart = new Rgba32(80, 78, 74, 120),
                ColorEnd = new Rgba32(40, 40, 38, 0),
                SizeStart = 0.2f,
                SizeEnd = 0.5f,
                Kind = (int)ParticleKind.Smoke,
                Drag = 1.2f,
            });
        }
    }

    public static void Explosion(SpriteParticleField field, Vector3 origin, float scale)
    {
        for (var ring = 0; ring < 4; ring++)
        {
            var n = 18 + ring * 10;
            for (var i = 0; i < n; i++)
            {
                var a = i / (float)n * MathF.PI * 2f;
                var speed = (4f + ring * 2.2f) * scale;
                field.Emit(new SpriteParticle
                {
                    Position = origin,
                    Velocity = Vector3PlanarExtensions.FromHeadingXz(a) * speed,
                    Elevation = 0.3f,
                    ElevationVelocity = 2f + ring,
                    MaxLife = 0.4f + ring * 0.12f,
                    ColorStart = ring == 0
                        ? new Rgba32(255, 246, 190, 255)
                        : new Rgba32(255, (byte)(90 + ring * 20), 24, 230),
                    ColorEnd = new Rgba32(40, 16, 8, 0),
                    SizeStart = (0.22f + ring * 0.1f) * scale,
                    SizeEnd = (0.45f + ring * 0.18f) * scale,
                    Kind = ring == 0 ? (int)ParticleKind.Spark : (int)ParticleKind.SoftGlow,
                    Drag = 2.2f,
                });
            }
        }

        for (var i = 0; i < (int)(18 * scale); i++)
        {
            field.Emit(new SpriteParticle
            {
                Position = origin,
                Velocity = new Vector3((Random.Shared.NextSingle() - 0.5f) * 3f, 0f, (Random.Shared.NextSingle() - 0.5f) * 3f),
                Elevation = 0.4f,
                ElevationVelocity = 1f,
                MaxLife = 0.8f,
                ColorStart = new Rgba32(70, 68, 64, 160),
                ColorEnd = new Rgba32(30, 30, 28, 0),
                SizeStart = 0.4f * scale,
                SizeEnd = 1.2f * scale,
                Kind = (int)ParticleKind.Smoke,
                Drag = 1.1f,
            });
        }
    }

    public static void Chunks(SpriteParticleField field, Vector3 origin, float scale)
    {
        var n = (int)(14 * scale);
        for (var i = 0; i < n; i++)
        {
            var a = Random.Shared.NextSingle() * MathF.PI * 2f;
            var speed = 1.5f + Random.Shared.NextSingle() * 4f;
            field.Emit(new SpriteParticle
            {
                Position = origin,
                Velocity = Vector3PlanarExtensions.FromHeadingXz(a) * speed,
                Elevation = 0.35f,
                ElevationVelocity = 2.5f + Random.Shared.NextSingle() * 3f,
                MaxLife = 0.7f,
                ColorStart = new Rgba32(110, 30, 28, 255),
                ColorEnd = new Rgba32(50, 12, 12, 180),
                SizeStart = 0.14f * scale,
                SizeEnd = 0.1f * scale,
                Kind = (int)ParticleKind.Debris,
                Drag = 1.8f,
                Spin = 8f,
            });
        }
    }

    public static void AmbientDust(SpriteParticleField field, Vector3 around)
    {
        field.Emit(new SpriteParticle
        {
            Position = around + new Vector3((Random.Shared.NextSingle() - 0.5f) * 10f, 0f, (Random.Shared.NextSingle() - 0.5f) * 10f),
            Velocity = new Vector3((Random.Shared.NextSingle() - 0.5f) * 0.3f, 0f, 0.15f),
            Elevation = Random.Shared.NextSingle() * 1.4f,
            ElevationVelocity = 0.05f,
            MaxLife = 1.6f,
            ColorStart = new Rgba32(180, 170, 140, 50),
            ColorEnd = new Rgba32(80, 76, 60, 0),
            SizeStart = 0.08f,
            SizeEnd = 0.18f,
            Kind = (int)ParticleKind.Ember,
            Drag = 0.4f,
        });
    }
}
