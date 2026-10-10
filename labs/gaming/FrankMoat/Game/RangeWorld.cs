using System.Numerics;
using FrankMoat.Actors;
using FrankMoat.Combat;
using FrankMoat.Input;
using FrankMoat.Levels;
using FrankMoat.Narrative;
using FrankMoat.Particles;
using FrankMoat.Weapons;
using Novolis.Math.Geometry;
using Novolis.Rendering.Planar;

namespace FrankMoat.Game;

internal sealed class RangeWorld
{
    public FrankActor Frank { get; } = new();
    public WeaponState Weapon { get; } = new(WeaponCatalog.M1911);
    public List<UndeadActor> Undead { get; } = [];
    public List<RangeProjectile> Projectiles { get; } = [];
    public List<RangePickup> Pickups { get; } = [];
    public List<RangeBarrel> Barrels { get; } = [];
    public List<RaisedWall> Walls { get; } = [];
    public List<WallSegment> Segments { get; } = [];
    public SpriteParticleField Particles { get; } = new();
    public SpriteDecalField Decals { get; } = new();
    public RangeJuice Juice { get; } = new();
    public FaithAnnouncer Faith { get; } = new();
    public bool OwnsPump { get; private set; }
    public bool OwnsRotary { get; private set; }
    public bool Inspecting { get; set; }
    public bool Cleared { get; private set; }
    public int Wave { get; private set; }
    public int Kills { get; private set; }
    public float WaveDelay { get; private set; }
    public float AmbientTimer { get; private set; }

    private readonly Dictionary<WeaponId, (int Reserve, int Mag, bool Chambered)> _stash = [];

    public void Reset()
    {
        Undead.Clear();
        Projectiles.Clear();
        Pickups.Clear();
        Barrels.Clear();
        Walls.Clear();
        Segments.Clear();
        Particles.Clear();
        Decals.Clear();
        Juice.Clear();
        OwnsPump = false;
        OwnsRotary = false;
        Inspecting = false;
        Cleared = false;
        Wave = 0;
        Kills = 0;
        WaveDelay = 1.2f;
        Weapon.Equip(WeaponCatalog.M1911);
        Weapon.Magazine = Weapon.Spec.MagazineSize;
        Weapon.Chambered = true;
        Weapon.Reserve = Weapon.Spec.ReserveStart;
        Frank.Health = 100;
        Frank.IFrames = 0f;
        _stash.Clear();
        Faith.Reset();
    }

    public void Tick(float dt, PlanarCollisionWorld collision, RangeInput input, Vector3 aimWorld)
    {
        if (Inspecting)
        {
            return;
        }

        if (aimWorld.LengthSquaredXz() > 0.0001f)
        {
            Frank.Facing = aimWorld.NormalizeXz();
        }

        var move = new Vector3(input.MoveX, 0f, input.MoveZ);
        if (move.LengthSquaredXz() > 1f)
        {
            move = move.NormalizeXz();
        }

        var delta = move * FrankActor.Speed * dt;
        Frank.Position = collision.MoveCircle(Frank.Position, delta, FrankActor.Radius);
        Frank.IFrames = MathF.Max(0f, Frank.IFrames - dt);

        HandleWeaponSelect(input);
        TickWeapon(dt, input);
        TickProjectiles(dt);
        TickUndead(dt, collision);
        TickBarrels();
        TickPickups();
        TickWaves(dt);
        Particles.Tick(dt);
        Juice.Tick(dt);
        Faith.Tick(dt, Wave, Weapon.Spec.Id, Undead.Count);

        AmbientTimer -= dt;
        if (AmbientTimer <= 0f)
        {
            AmbientTimer = 0.045f;
            ParticleEmit.AmbientDust(Particles, Frank.Position);
        }
    }

    public void SelectWeapon(WeaponId id)
    {
        if (id == WeaponId.Pump12 && !OwnsPump)
        {
            return;
        }

        if (id == WeaponId.Rotary && !OwnsRotary)
        {
            return;
        }

        if (Weapon.Spec.Id == id)
        {
            return;
        }

        var reserve = Weapon.Reserve;
        var mag = Weapon.Magazine;
        var chambered = Weapon.Chambered;
        var previous = Weapon.Spec.Id;
        Stash(previous, reserve, mag, chambered);
        Weapon.Equip(WeaponCatalog.FromId(id));
        Restore(id);
    }

    private void Stash(WeaponId id, int reserve, int mag, bool chambered) =>
        _stash[id] = (reserve, mag, chambered);

    private void Restore(WeaponId id)
    {
        if (_stash.TryGetValue(id, out var saved))
        {
            Weapon.Reserve = saved.Reserve;
            Weapon.Magazine = saved.Mag;
            Weapon.Chambered = saved.Chambered;
            return;
        }

        Weapon.Magazine = Weapon.Spec.MagazineSize;
        Weapon.Chambered = Weapon.Spec.ChamberPlusOne;
        Weapon.Reserve = Weapon.Spec.ReserveStart;
    }

    private void HandleWeaponSelect(RangeInput input)
    {
        if (input.Hotkey == WeaponHotkey.One)
        {
            SelectWeapon(WeaponId.M1911);
        }
        else if (input.Hotkey == WeaponHotkey.Two)
        {
            SelectWeapon(WeaponId.Pump12);
        }
        else if (input.Hotkey == WeaponHotkey.Three)
        {
            SelectWeapon(WeaponId.Rotary);
        }
        else if (input.CyclePressed)
        {
            CycleWeapon();
        }
    }

    private void CycleWeapon()
    {
        var order = new List<WeaponId> { WeaponId.M1911 };
        if (OwnsPump)
        {
            order.Add(WeaponId.Pump12);
        }

        if (OwnsRotary)
        {
            order.Add(WeaponId.Rotary);
        }

        var idx = order.IndexOf(Weapon.Spec.Id);
        SelectWeapon(order[(idx + 1) % order.Count]);
    }

    private void TickWeapon(float dt, RangeInput input)
    {
        Weapon.Cooldown = MathF.Max(0f, Weapon.Cooldown - dt);
        Weapon.TickReload(dt);

        var spec = Weapon.Spec;
        if (spec.Mode == WeaponFireMode.Rotary && !input.ShootHeld)
        {
            Weapon.Spin = MathF.Max(0f, Weapon.Spin - dt / MathF.Max(0.12f, spec.SpinUpSeconds));
        }

        if (input.ReloadPressed)
        {
            Weapon.BeginReload();
        }

        var wantsShot = spec.Mode == WeaponFireMode.Semi ? input.ShootPressed : input.ShootHeld;
        if (!wantsShot)
        {
            return;
        }

        if (Weapon.Reloading && spec.TubeFeed && Weapon.RoundsInGun > 0)
        {
            Weapon.InterruptReload();
        }

        if (spec.Mode == WeaponFireMode.Rotary)
        {
            Weapon.Spin = MathF.Min(1f, Weapon.Spin + dt / spec.SpinUpSeconds);
            if (Weapon.Spin < 1f)
            {
                return;
            }
        }

        if (!Weapon.CanFire)
        {
            if (Weapon.RoundsInGun <= 0)
            {
                Weapon.BeginReload();
            }

            return;
        }

        Fire();
    }

    private void Fire()
    {
        var spec = Weapon.Spec;
        Weapon.ConsumeRound();
        Weapon.Cooldown = spec.FireIntervalSeconds;
        Juice.AddShake(spec.RecoilShake);
        Juice.FlashMuzzle();
        ParticleEmit.Muzzle(Particles, Frank.Position, Frank.Facing, spec);

        var baseAngle = Frank.Facing.Atan2Xz();
        for (var i = 0; i < spec.PelletCount; i++)
        {
            var spread = spec.SpreadDegrees * (MathF.PI / 180f);
            var angle = baseAngle + (Random.Shared.NextSingle() - 0.5f) * spread;
            var dir = Vector3PlanarExtensions.FromHeadingXz(angle);
            Projectiles.Add(new RangeProjectile
            {
                Position = Frank.Position + dir * 0.48f,
                Velocity = dir * spec.ProjectileSpeed,
                Damage = spec.Damage,
            });
        }
    }

    private void TickProjectiles(float dt)
    {
        for (var i = Projectiles.Count - 1; i >= 0; i--)
        {
            var p = Projectiles[i];
            p.Life -= dt;
            var speed = p.Velocity.Length();
            if (p.Life <= 0f || speed < 0.01f)
            {
                Projectiles.RemoveAt(i);
                continue;
            }

            var dir = p.Velocity / speed;
            var travel = speed * dt;
            if (TryHitscan(p, dir, travel))
            {
                Projectiles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * dt;
            if (Random.Shared.NextSingle() < 0.7f)
            {
                ParticleEmit.Tracer(Particles, p.Position, p.FromFrank);
            }
        }
    }

    private bool TryHitscan(RangeProjectile projectile, Vector3 dir, float travel)
    {
        var best = travel + 1f;
        PlanarHit? wall = null;
        UndeadActor? undead = null;
        RangeBarrel? barrel = null;

        foreach (var segment in Segments)
        {
            if (PlanarRay.TryHitSegment(projectile.Position, dir, travel, segment.Start, segment.End, out var hit)
                && hit.Distance < best)
            {
                best = hit.Distance;
                wall = hit;
                undead = null;
                barrel = null;
            }
        }

        foreach (var foe in Undead)
        {
            if (PlanarRay.TryHitCircle(projectile.Position, dir, travel, foe.Position, foe.Radius, out var d)
                && d < best)
            {
                best = d;
                undead = foe;
                wall = null;
                barrel = null;
            }
        }

        foreach (var drum in Barrels)
        {
            if (!drum.Alive)
            {
                continue;
            }

            if (PlanarRay.TryHitCircle(projectile.Position, dir, travel, drum.Position, 0.38f, out var d)
                && d < best)
            {
                best = d;
                barrel = drum;
                wall = null;
                undead = null;
            }
        }

        if (undead is not null)
        {
            HurtUndead(undead, projectile.Damage, dir);
            return true;
        }

        if (barrel is not null)
        {
            barrel.Health -= projectile.Damage;
            ParticleEmit.WallImpact(Particles, barrel.Position, dir, steel: false);
            if (!barrel.Alive)
            {
                ExplodeBarrel(barrel);
            }

            return true;
        }

        if (wall is { } wh)
        {
            var steel = NearestMaterial(wh.Point) == WallMaterial.Steel;
            ParticleEmit.WallImpact(Particles, wh.Point, wh.Normal, steel);
            Decals.Add(new SpriteDecal
            {
                Position = wh.Point + wh.Normal * 0.02f,
                Elevation = 0.55f + Random.Shared.NextSingle() * 1.4f,
                Width = 0.18f + Random.Shared.NextSingle() * 0.16f,
                Height = 0.18f,
                Rotation = Random.Shared.NextSingle() * MathF.PI,
                Color = steel ? new Rgba32(40, 38, 36, 180) : new Rgba32(28, 26, 24, 160),
                Kind = (int)DecalKind.SparkMark,
            });
            return true;
        }

        return false;
    }

    private WallMaterial NearestMaterial(Vector3 point)
    {
        var best = float.MaxValue;
        var material = WallMaterial.Concrete;
        foreach (var segment in Segments)
        {
            var mid = (segment.Start + segment.End) * 0.5f;
            var d = Vector3.DistanceSquared(mid, point);
            if (d < best)
            {
                best = d;
                material = segment.Material;
            }
        }

        return material;
    }

    private void HurtUndead(UndeadActor foe, int damage, Vector3 incoming)
    {
        foe.Health -= damage;
        foe.HitFlash = 0.08f;
        ParticleEmit.Blood(Particles, foe.Position, incoming, foe.Kind == UndeadKind.Tank ? 1.6f : 1f);
        Decals.Add(new SpriteDecal
        {
            Position = foe.Position + incoming * 0.15f,
            Width = 0.35f + Random.Shared.NextSingle() * 0.35f,
            Height = 0.22f,
            Rotation = Random.Shared.NextSingle() * MathF.PI,
            Color = new Rgba32(120, 10, 14, 150),
            Kind = (int)DecalKind.BloodFloor,
        });

        if (foe.Health > 0)
        {
            return;
        }

        Kills++;
        ParticleEmit.Chunks(Particles, foe.Position, foe.Kind == UndeadKind.Tank ? 2.2f : 1f);
        ParticleEmit.Blood(Particles, foe.Position, incoming, 1.8f);
        Decals.Add(new SpriteDecal
        {
            Position = foe.Position,
            Width = 0.9f,
            Height = 0.55f,
            Rotation = Random.Shared.NextSingle() * MathF.PI,
            Color = new Rgba32(90, 8, 12, 200),
            Kind = (int)DecalKind.BloodFloor,
        });
        Undead.Remove(foe);
    }

    private void ExplodeBarrel(RangeBarrel barrel)
    {
        ParticleEmit.Explosion(Particles, barrel.Position, 1.35f);
        Juice.AddShake(0.55f);
        Decals.Add(new SpriteDecal
        {
            Position = barrel.Position,
            Width = 1.6f,
            Height = 1.6f,
            Color = new Rgba32(28, 18, 12, 210),
            Kind = (int)DecalKind.Scorch,
        });

        for (var i = Undead.Count - 1; i >= 0; i--)
        {
            var foe = Undead[i];
            if (Vector3PlanarExtensions.DistanceXz(foe.Position, barrel.Position) < 3.4f)
            {
                HurtUndead(foe, 80, foe.Position - barrel.Position);
            }
        }

        if (Vector3PlanarExtensions.DistanceXz(Frank.Position, barrel.Position) < 2.6f)
        {
            HurtFrank(28);
        }
    }

    private void TickUndead(float dt, PlanarCollisionWorld collision)
    {
        foreach (var foe in Undead)
        {
            foe.HitFlash = MathF.Max(0f, foe.HitFlash - dt);
            var to = Frank.Position - foe.Position;
            var dist = to.LengthXz();
            if (dist > 0.01f)
            {
                foe.Facing = to.NormalizeXz();
            }

            if (dist > foe.Radius + FrankActor.Radius + 0.08f)
            {
                var step = foe.Facing * foe.Speed * dt;
                foe.Position = collision.MoveCircle(foe.Position, step, foe.Radius);
                continue;
            }

            foe.AttackTimer -= dt;
            if (foe.AttackTimer > 0f)
            {
                continue;
            }

            foe.AttackTimer = foe.Kind == UndeadKind.Tank ? 0.85f : 0.55f;
            HurtFrank(foe.Kind == UndeadKind.Tank ? 22 : 10);
        }
    }

    private void HurtFrank(int amount)
    {
        if (Frank.IFrames > 0f)
        {
            return;
        }

        Frank.Health = Math.Max(0, Frank.Health - amount);
        Frank.IFrames = 0.35f;
        Juice.AddShake(0.28f);
        ParticleEmit.Blood(Particles, Frank.Position, -Frank.Facing, 0.6f);
    }

    private void TickBarrels()
    {
        for (var i = Barrels.Count - 1; i >= 0; i--)
        {
            if (!Barrels[i].Alive)
            {
                Barrels.RemoveAt(i);
            }
        }
    }

    private void TickPickups()
    {
        foreach (var pickup in Pickups)
        {
            if (pickup.Taken || Vector3PlanarExtensions.DistanceXz(Frank.Position, pickup.Position) > 0.7f)
            {
                continue;
            }

            pickup.Taken = true;
            switch (pickup.Kind)
            {
                case PickupKind.Ammo45:
                    AddReserve(WeaponId.M1911, 28);
                    Faith.Cue.Say("Reserve .45. Frank approves.");
                    break;
                case PickupKind.Ammo12:
                    AddReserve(WeaponId.Pump12, 16);
                    break;
                case PickupKind.Ammo762:
                    AddReserve(WeaponId.Rotary, 200);
                    break;
                case PickupKind.Health:
                    Frank.Health = Math.Min(100, Frank.Health + 40);
                    Faith.Cue.Say("Medkit. Facility inventory still works. That is almost funny.");
                    break;
                case PickupKind.Pump12:
                    OwnsPump = true;
                    SelectWeapon(WeaponId.Pump12);
                    Faith.Cue.Say("Pump-action recovered from a range bag. Facilities would be proud.");
                    break;
                case PickupKind.Rotary:
                    OwnsRotary = true;
                    SelectWeapon(WeaponId.Rotary);
                    Faith.Cue.Say("HRRS rotary is on the line. Engineering's league would call this a warm-up.");
                    break;
            }
        }
    }

    private void AddReserve(WeaponId id, int amount)
    {
        if (Weapon.Spec.Id == id)
        {
            Weapon.Reserve += amount;
            return;
        }

        if (_stash.TryGetValue(id, out var saved))
        {
            _stash[id] = (saved.Reserve + amount, saved.Mag, saved.Chambered);
            return;
        }

        var spec = WeaponCatalog.FromId(id);
        _stash[id] = (spec.ReserveStart + amount, spec.MagazineSize, spec.ChamberPlusOne);
    }

    private void TickWaves(float dt)
    {
        if (Cleared || Frank.Health <= 0)
        {
            return;
        }

        if (Undead.Count > 0)
        {
            return;
        }

        WaveDelay -= dt;
        if (WaveDelay > 0f)
        {
            return;
        }

        if (Wave >= 3)
        {
            Cleared = true;
            Faith.Cue.Say("Range is quiet. Directive 4761 still locks the express elevator.", 5f);
            return;
        }

        Wave++;
        WaveDelay = 2.4f;
        SpawnWave(Wave);
    }

    private void SpawnWave(int wave)
    {
        var shamblers = wave switch { 1 => 6, 2 => 10, _ => 14 };
        var runners = wave switch { 1 => 0, 2 => 3, _ => 5 };
        var tanks = wave >= 3 ? 1 : 0;
        for (var i = 0; i < shamblers; i++)
        {
            Undead.Add(UndeadActor.Create(UndeadKind.Shambler, SpawnPoint(i)));
        }

        for (var i = 0; i < runners; i++)
        {
            Undead.Add(UndeadActor.Create(UndeadKind.Runner, SpawnPoint(20 + i)));
        }

        for (var i = 0; i < tanks; i++)
        {
            Undead.Add(UndeadActor.Create(UndeadKind.Tank, new Vector3(42f, 0f, 44f)));
        }
    }

    private static Vector3 SpawnPoint(int index)
    {
        var t = index / 8f;
        var x = 6f + (index % 8) * 5.2f;
        var z = 44f - (index / 8) * 2.4f;
        return new Vector3(x + MathF.Sin(t) * 0.4f, 0f, z);
    }

}
