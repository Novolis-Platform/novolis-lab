using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;
using TopDownDoom.Design;
using TopDownDoom.Game.ImportedDoom;

namespace TopDownDoom.Game;

internal sealed class TopDownCombatWorld
{
    public Vector3 PlayerPosition { get; set; }
    public Vector2 PlayerVelocity { get; set; }
    public float PlayerFacingRadians { get; set; }
    public int Health { get; set; } = 100;
    public int Armor { get; set; }
    public int Ammo { get; set; } = 48;
    public WeaponSpec ActiveWeapon { get; set; } = WeaponCatalog.Pistol;
    public TimeSpan FireCooldown { get; set; }
    public TimeSpan DashCooldown { get; set; }
    public bool HasBlueKey { get; set; }
    public bool ExitUnlocked { get; set; }
    public bool ClosetAmbushTriggered { get; set; }
    public bool FoundShotgun { get; set; }
    public float DashTrailTimer { get; set; }

    public readonly CombatJuice Juice = new();
    public readonly List<Projectile> Projectiles = [];
    public readonly List<Monster> Monsters = [];
    public readonly List<Pickup> Pickups = [];
    public readonly List<ExplosiveBarrel> Barrels = [];
    private Random _random = new(4761);
    private PlanarCollisionWorld? _collision;
    private IReadOnlyList<ImportedDoomWallSegment> _wallSegments = Array.Empty<ImportedDoomWallSegment>();
    private ImportedDoomWallIndex? _wallIndex;

    public void SetGeometry(
        PlanarCollisionWorld collision,
        IReadOnlyList<ImportedDoomWallSegment> wallSegments)
    {
        _collision = collision ?? throw new ArgumentNullException(nameof(collision));
        _wallSegments = wallSegments ?? throw new ArgumentNullException(nameof(wallSegments));
        _wallIndex = new ImportedDoomWallIndex(_wallSegments);
    }

    public void Reset()
    {
        PlayerPosition = Vector3.Zero;
        PlayerVelocity = Vector2.Zero;
        PlayerFacingRadians = 0f;
        Health = 100;
        Armor = 0;
        Ammo = 48;
        ActiveWeapon = WeaponCatalog.Pistol;
        FireCooldown = TimeSpan.Zero;
        DashCooldown = TimeSpan.Zero;
        HasBlueKey = false;
        ExitUnlocked = false;
        ClosetAmbushTriggered = false;
        FoundShotgun = false;
        DashTrailTimer = 0f;
        _random = new Random(4761);
        Projectiles.Clear();
        Monsters.Clear();
        Pickups.Clear();
        Barrels.Clear();
        Juice.Clear();
    }

    public void SeedImportedLevel(ImportedDoomLevelBuild level)
    {
        ArgumentNullException.ThrowIfNull(level);
        PlayerPosition = level.PlayerStart;
        PlayerFacingRadians = level.PlayerAngle * (MathF.PI / 180f);
        foreach (var spawn in level.Things)
        {
            if (DoomThingCatalog.IsPlayerStart(spawn.Thing.Type))
            {
                continue;
            }

            if (DoomThingCatalog.TryMonsterRole(spawn.Thing.Type) is { } role)
            {
                SpawnMonster(role, spawn.Position.X, spawn.Position.Z);
                continue;
            }

            if (DoomThingCatalog.TryPickup(spawn.Thing.Type) is { } pickup)
            {
                Pickups.Add(new Pickup(pickup, spawn.Position));
            }
        }

        foreach (var exit in level.ExitPositions)
        {
            Pickups.Add(new Pickup(PickupKind.Exit, exit));
        }
    }

    public void Tick(float dt, Vector2 moveInput, Vector2 aimWorld, bool shootHeld, bool dashPressed)
    {
        FireCooldown -= TimeSpan.FromSeconds(dt);
        DashCooldown -= TimeSpan.FromSeconds(dt);
        DashTrailTimer -= dt;

        if (dashPressed && DashCooldown <= TimeSpan.Zero && moveInput.LengthSquared() > 0.01f)
        {
            var dash = Vector2.Normalize(moveInput) * 4.2f;
            PlayerVelocity += dash;
            DashCooldown = TimeSpan.FromSeconds(2.8);
            ParticlePresets.DashDust(Juice.Particles, PlayerPosition, moveInput);
            for (var i = 0; i < 8; i++)
            {
                ParticlePresets.DashDust(Juice.Particles, PlayerPosition, moveInput);
            }

            Juice.AddShake(0.2f);
        }

        if (DashTrailTimer <= 0f && PlayerVelocity.LengthSquared() > 12f)
        {
            DashTrailTimer = 0.04f;
            ParticlePresets.DashDust(Juice.Particles, PlayerPosition, new Vector2(PlayerVelocity.X, PlayerVelocity.Y));
        }

        PlayerVelocity = DoomMovement.Integrate(PlayerVelocity, moveInput, dt);
        if (_collision is not null)
        {
            PlayerPosition = _collision.MoveCircle(
                PlayerPosition,
                new Vector3(PlayerVelocity.X * dt, 0f, PlayerVelocity.Y * dt),
                0.35f);
        }

        if (aimWorld.LengthSquared() > 0.01f)
        {
            PlayerFacingRadians = MathF.Atan2(aimWorld.Y, aimWorld.X);
        }

        if (shootHeld && FireCooldown <= TimeSpan.Zero && Ammo > 0)
        {
            FireWeapon();
        }

        TickProjectiles(dt);
        TickMonsters(dt);
        TickBarrels(dt);
        ResolvePickups();
        Juice.Tick(dt);
    }

    private void FireWeapon()
    {
        var weapon = ActiveWeapon;
        FireCooldown = weapon.FireInterval;
        Ammo = Math.Max(0, Ammo - 1);

        ParticlePresets.MuzzleFlash(Juice.Particles, PlayerPosition, PlayerFacingRadians, weapon);
        Juice.AddShake(weapon.PelletCount > 4 ? 0.14f : 0.07f);

        for (var i = 0; i < weapon.PelletCount; i++)
        {
            var spread = weapon.SpreadDegrees * (MathF.PI / 180f);
            var angle = PlayerFacingRadians + _random.NextSingle() * spread - spread * 0.5f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            Projectiles.Add(new Projectile(
                PlayerPosition + new Vector3(dir.X * 0.4f, 0f, dir.Y * 0.4f),
                new Vector3(dir.X * weapon.ProjectileSpeed, 0f, dir.Y * weapon.ProjectileSpeed),
                weapon.Damage,
                weapon.SplashRadius,
                weapon.CausesSelfDamage,
                true));
        }
    }

    private void TickProjectiles(float dt)
    {
        for (var i = Projectiles.Count - 1; i >= 0; i--)
        {
            var p = Projectiles[i];
            p.Life -= dt;
            var velocityLength = p.Velocity.Length();
            var direction = velocityLength > 0.001f ? p.Velocity / velocityLength : Vector3.Zero;
            var travel = velocityLength * dt;
            if (travel > 0f && TryHitWall(p.Position, direction, travel, out var wallHit))
            {
                p.Position = wallHit.Point;
                ParticlePresets.HitSparks(Juice.Particles, wallHit.Point, new Rgba32(180, 180, 180, 220), 5);
                Projectiles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * dt;

            var rocket = p.SplashRadius > 1.5f;
            if (_random.NextSingle() < (rocket ? 0.9f : 0.55f))
            {
                ParticlePresets.BulletTrail(Juice.Particles, p.Position, p.FromPlayer, rocket);
            }

            if (p.Life <= 0f)
            {
                if (p.SplashRadius > 0f)
                {
                    Juice.Boom(p.Position, p.SplashRadius > 2f ? 1.4f : 1f);
                }

                Projectiles.RemoveAt(i);
                continue;
            }

            if (p.SplashRadius > 0f)
            {
                var detonate = false;
                for (var m = 0; m < Monsters.Count; m++)
                {
                    if (DistanceXZ(p.Position, Monsters[m].Position) < p.SplashRadius * 0.45f)
                    {
                        detonate = true;
                        break;
                    }
                }

                for (var b = 0; b < Barrels.Count; b++)
                {
                    if (DistanceXZ(p.Position, Barrels[b].Position) < 0.6f)
                    {
                        detonate = true;
                        break;
                    }
                }

                if (detonate)
                {
                    TrySplash(p, includePlayer: p.CausesSelfDamage);
                    Projectiles.RemoveAt(i);
                }
            }
            else
            {
                for (var m = Monsters.Count - 1; m >= 0; m--)
                {
                    if (DistanceXZ(p.Position, Monsters[m].Position) < 0.45f)
                    {
                        DamageMonster(m, p.Damage, p.Position);
                        ParticlePresets.HitSparks(Juice.Particles, p.Position, new Rgba32(255, 220, 100, 255), 8);
                        Projectiles.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }

    private void TrySplash(Projectile p, bool includePlayer)
    {
        Juice.Boom(p.Position, p.SplashRadius > 2f ? 1.5f : 1.1f);
        var hit = false;
        for (var m = Monsters.Count - 1; m >= 0; m--)
        {
            if (DistanceXZ(p.Position, Monsters[m].Position) <= p.SplashRadius)
            {
                DamageMonster(m, p.Damage, p.Position);
                hit = true;
            }
        }

        for (var b = Barrels.Count - 1; b >= 0; b--)
        {
            if (DistanceXZ(p.Position, Barrels[b].Position) <= p.SplashRadius)
            {
                DetonateBarrel(b);
                hit = true;
            }
        }

        if (includePlayer && DistanceXZ(p.Position, PlayerPosition) <= p.SplashRadius)
        {
            ApplyPlayerDamage(p.Damage / 2);
        }

        if (hit || p.Life <= 0f)
        {
            Projectiles.Remove(p);
        }
    }

    private void TickMonsters(float dt)
    {
        for (var i = Monsters.Count - 1; i >= 0; i--)
        {
            var monster = Monsters[i];
            if (monster.Health <= 0)
            {
                Monsters.RemoveAt(i);
                continue;
            }

            var toPlayer = PlayerPosition - monster.Position;
            var flat = new Vector2(toPlayer.X, toPlayer.Z);
            var dist = flat.Length();
            if (dist > 0.05f)
            {
                flat /= dist;
            }

            monster.FireCooldown -= TimeSpan.FromSeconds(dt);
            switch (monster.Role)
            {
                case MonsterRole.Fodder:
                    MoveMonster(monster, flat, 3.2f, dt);
                    break;
                case MonsterRole.Projectile:
                    MoveMonster(monster, flat, 2.4f, dt);
                    if (dist < 14f && monster.FireCooldown <= TimeSpan.Zero)
                    {
                        ShootFrom(monster, flat, 10f, 12);
                        monster.FireCooldown = TimeSpan.FromSeconds(1.4);
                    }
                    break;
                case MonsterRole.Charger:
                    MoveMonster(monster, flat, 6.5f, dt);
                    if (_random.NextSingle() < 0.15f)
                    {
                        ParticlePresets.HitSparks(Juice.Particles, monster.Position, new Rgba32(255, 120, 60, 120), 2);
                    }
                    break;
                case MonsterRole.Hitscan:
                    if (dist < 16f && monster.FireCooldown <= TimeSpan.Zero && HasLineOfSight(monster.Position))
                    {
                        ParticlePresets.HitscanBeam(Juice.Particles, monster.Position, PlayerPosition);
                        ApplyPlayerDamage(6);
                        monster.FireCooldown = TimeSpan.FromSeconds(0.9);
                    }
                    break;
                case MonsterRole.Bruiser:
                    MoveMonster(monster, flat, 2f, dt);
                    break;
                case MonsterRole.Flier:
                    MoveMonster(monster, flat, 2.8f, dt);
                    break;
                case MonsterRole.Boss:
                    MoveMonster(monster, flat, 1.4f, dt);
                    break;
                case MonsterRole.Summoner:
                    MoveMonster(monster, flat, 1.2f, dt);
                    break;
            }
        }
    }

    private void ShootFrom(Monster monster, Vector2 dir, float speed, int damage)
    {
        ParticlePresets.EnemyShot(Juice.Particles, monster.Position, dir);
        Projectiles.Add(new Projectile(
            monster.Position,
            new Vector3(dir.X * speed, 0f, dir.Y * speed),
            damage,
            0f,
            false,
            false));
    }

    private bool HasLineOfSight(Vector3 from)
    {
        var toPlayer = PlayerPosition - from;
        var distance = toPlayer.LengthXz();
        if (distance >= 18f || distance <= 0.01f)
        {
            return false;
        }

        var direction = toPlayer.NormalizeXz();
        return !TryHitWall(from, direction, distance - 0.05f, out _);
    }

    private void TickBarrels(float dt)
    {
        foreach (var barrel in Barrels)
        {
            if (_random.NextSingle() < 0.08f)
            {
                ParticlePresets.BarrelFuse(Juice.Particles, barrel.Position);
            }
        }

        for (var i = Barrels.Count - 1; i >= 0; i--)
        {
            if (Barrels[i].Fuse <= 0f)
            {
                continue;
            }

            Barrels[i].Fuse -= dt;
            if (Barrels[i].Fuse <= 0f)
            {
                DetonateBarrel(i);
            }
        }
    }

    private void DetonateBarrel(int index)
    {
        if (index < 0 || index >= Barrels.Count)
        {
            return;
        }

        var barrel = Barrels[index];
        Barrels.RemoveAt(index);
        Juice.Boom(barrel.Position, 1.35f);
        Projectiles.Add(new Projectile(barrel.Position, Vector3.Zero, 55, 2.4f, true, false)
        {
            Life = 0.01f,
        });
    }

    private void DamageMonster(int index, int damage, Vector3 hitPos)
    {
        var monster = Monsters[index];
        monster.Health -= damage;
        ParticlePresets.HitSparks(Juice.Particles, hitPos, new Rgba32(255, 200, 80, 255), 10);
        if (monster.Health > 0)
        {
            return;
        }

        var pos = monster.Position;
        var big = monster.Role is MonsterRole.Bruiser or MonsterRole.Charger;
        ParticlePresets.BloodSpray(Juice.Particles, pos, big ? 1.3f : 1f);
        Juice.Boom(pos, big ? 1.15f : 0.85f);
        if (monster.Role == MonsterRole.Charger)
        {
            Juice.Boom(pos, 1.2f);
            Projectiles.Add(new Projectile(pos, Vector3.Zero, 28, 1.6f, true, false) { Life = 0.02f });
        }

        Monsters.RemoveAt(index);
    }

    private void ResolvePickups()
    {
        for (var i = Pickups.Count - 1; i >= 0; i--)
        {
            if (DistanceXZ(PlayerPosition, Pickups[i].Position) > 0.7f)
            {
                continue;
            }

            var kind = Pickups[i].Kind;
            var pos = Pickups[i].Position;
            switch (kind)
            {
                case PickupKind.Health:
                    Health = Math.Min(100, Health + 25);
                    break;
                case PickupKind.Armor:
                    Armor = Math.Min(100, Armor + 20);
                    break;
                case PickupKind.Ammo:
                    Ammo += 24;
                    if (!FoundShotgun)
                    {
                        FoundShotgun = true;
                        ActiveWeapon = WeaponCatalog.Shotgun;
                    }
                    break;
                case PickupKind.BlueKey:
                    HasBlueKey = true;
                    TriggerClosetAmbush();
                    break;
                case PickupKind.Exit:
                    if (HasBlueKey)
                    {
                        ExitUnlocked = true;
                    }
                    break;
            }

            ParticlePresets.PickupBurst(Juice.Particles, pos, kind);
            Pickups.RemoveAt(i);
        }
    }

    public void TriggerClosetAmbush()
    {
        if (ClosetAmbushTriggered)
        {
            return;
        }

        ClosetAmbushTriggered = true;
        var spawns = new (MonsterRole role, float x, float z)[]
        {
            (MonsterRole.Fodder, 28f, 8f),
            (MonsterRole.Fodder, 30f, 10f),
            (MonsterRole.Projectile, 32f, 6f),
            (MonsterRole.Charger, 34f, 12f),
            (MonsterRole.Bruiser, 36f, 9f),
        };
        foreach (var (role, x, z) in spawns)
        {
            SpawnMonster(role, x, z);
            ParticlePresets.ClosetSpawn(Juice.Particles, Vector3PlanarExtensions.Xz(x, z));
        }

        Juice.AddShake(0.7f);
    }

    public void SpawnMonster(MonsterRole role, float x, float z)
    {
        var hp = role switch
        {
            MonsterRole.Bruiser => 80,
            MonsterRole.Charger => 35,
            MonsterRole.Projectile => 28,
            _ => 20,
        };
        Monsters.Add(new Monster(role, Vector3PlanarExtensions.Xz(x, z), hp));
    }

    private void MoveMonster(Monster monster, Vector2 direction, float speed, float dt)
    {
        var delta = new Vector3(direction.X * speed * dt, 0f, direction.Y * speed * dt);
        monster.Position = _collision?.MoveCircle(monster.Position, delta, 0.32f)
            ?? monster.Position + delta;
    }

    private bool TryHitWall(Vector3 origin, Vector3 direction, float length, out PlanarHit hit)
    {
        var nearest = length + 0.001f;
        hit = default;
        var found = false;
        foreach (var segment in _wallIndex?.Query(origin, direction, length) ?? _wallSegments)
        {
            if (!PlanarRay.TryHitSegment(origin, direction, length, segment.Start, segment.End, out var candidate)
                || candidate.Distance >= nearest)
            {
                continue;
            }

            nearest = candidate.Distance;
            hit = candidate;
            found = true;
        }

        return found;
    }

    private void ApplyPlayerDamage(int amount)
    {
        var absorbed = Math.Min(Armor, amount / 2);
        Armor -= absorbed;
        Health -= amount - absorbed;
        Health = Math.Max(0, Health);
        ParticlePresets.HitSparks(Juice.Particles, PlayerPosition, new Rgba32(255, 80, 80, 255), 12);
        ParticlePresets.BloodSpray(Juice.Particles, PlayerPosition, 0.5f);
        Juice.AddShake(0.35f);
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
