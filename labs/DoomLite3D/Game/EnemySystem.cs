using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Novolis.Simulation.World;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Raylib.Rendering;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace DoomLite3D.Game;

internal sealed class Enemy
{
    public Vector3 Position;
    public bool Alive = true;
    public EnemyKind Kind = EnemyKind.Grunt;
    public int SpriteIndex;
    public float Health = 30f;
    public float MaxHealth = 30f;
    public float HitRadius = 1.1f;
    public float MoveRadius = 0.55f;
    public float BillboardSize = 1.35f;
    public float ChaseSpeed = 1.8f;
    public float MeleeCooldown;
    public float RangedCooldown;
}

internal sealed class BossBolt
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float TimeToLive = 4f;
}

internal sealed class EnemySystem
{
    private const float GruntRadius = 0.55f;
    private const float BossRadius = 0.65f;
    private const float GruntBillboard = 1.35f;
    private const float BossBillboard = 2.1f;
    private const float GruntHealth = 30f;
    private const float BossHealth = 200f;
    private const float GruntChaseSpeed = 1.8f;
    private const float BossChaseSpeed = 1.0f;
    private const float MaxShootRange = 18f;
    private const float ChaseRange = 12f;
    private const float ShotLosClearance = 0.35f;
    private const float BossRangedMin = 5f;
    private const float BossRangedMax = 16f;
    private const float BossBoltSpeed = 5f;
    private const float BossBoltDamage = 10f;
    private const float BossRangedCooldown = 2.5f;
    private static readonly Color BossBoltColor = Color.FromArgb(255, 220, 80, 200);

    private readonly List<Enemy> _enemies = [];
    private readonly List<BossBolt> _bolts = [];
    private Texture[] _sprites = [];
    private bool _loggedMissingAssets;

    public IReadOnlyList<Enemy> Enemies => _enemies;
    public IReadOnlyList<BossBolt> Bolts => _bolts;

    public void Initialize(RayGameContext ctx)
    {
        _sprites =
        [
            TryLoad(ctx, "enemies", "imp.png"),
            TryLoad(ctx, "enemies", "demon.png"),
            TryLoad(ctx, "enemies", "brute.png"),
            TryLoad(ctx, "enemies", "boss.png"),
        ];

        if (!_loggedMissingAssets)
        {
            _loggedMissingAssets = true;
            for (var i = 0; i < _sprites.Length; i++)
            {
                if (!_sprites[i].IsValid)
                    Debug.WriteLine($"DoomLite3D: missing or invalid texture for enemy sprite index {i}");
            }
        }
    }

    public void Reset(LevelMap level)
    {
        _enemies.Clear();
        _bolts.Clear();
        foreach (var spawn in level.Enemies)
        {
            var isBoss = spawn.Kind == EnemyKind.Boss;
            var radius = isBoss ? BossRadius : GruntRadius;
            var world = level.CellToWorld(spawn.Cell.X, spawn.Cell.Y, 0.9f);
            var xz = new Vector3(world.X, 0f, world.Z);
            if (PlanarOccupancy.OverlapsWall(level.Walls, xz, radius, LevelMap.CellSize))
                continue;

            xz = PlanarOccupancy.PushOutOfWalls(level.Walls, xz, radius, LevelMap.CellSize);
            if (PlanarOccupancy.OverlapsWall(level.Walls, xz, radius, LevelMap.CellSize))
                continue;

            var maxHealth = isBoss ? BossHealth : GruntHealth;
            _enemies.Add(new Enemy
            {
                Position = new Vector3(xz.X, world.Y, xz.Z),
                Kind = spawn.Kind,
                SpriteIndex = spawn.SpriteIndex,
                Health = maxHealth,
                MaxHealth = maxHealth,
                HitRadius = isBoss ? 1.6f : 1.1f,
                MoveRadius = radius,
                BillboardSize = isBoss ? BossBillboard : GruntBillboard,
                ChaseSpeed = isBoss ? BossChaseSpeed : GruntChaseSpeed,
                Alive = true,
            });
        }
    }

    public void Update(
        RayGameContext ctx,
        LevelMap level,
        PlayerController player,
        PlayerCombatState combat)
    {
        if (combat.IsDead)
            return;

        var dt = ctx.DeltaSeconds;
        var playerPos = player.Camera.Position;
        var playerXZ = new Vector3(playerPos.X, 0f, playerPos.Z);

        foreach (var enemy in _enemies)
        {
            if (!enemy.Alive)
                continue;

            enemy.MeleeCooldown = Math.Max(0f, enemy.MeleeCooldown - dt);
            enemy.RangedCooldown = Math.Max(0f, enemy.RangedCooldown - dt);

            var enemyXZ = new Vector3(enemy.Position.X, 0f, enemy.Position.Z);
            var toPlayer = playerXZ - enemyXZ;
            var dist = toPlayer.Length();
            var losClearance = enemy.MoveRadius;
            var hasLos = PlanarOccupancy.HasLineOfSight(
                level.Walls,
                enemyXZ,
                playerXZ,
                LevelMap.CellSize,
                losClearance);

            if (hasLos && dist > 0.05f && dist < ChaseRange)
            {
                var step = Vector3.Normalize(toPlayer) * (enemy.ChaseSpeed * dt);
                var moved = TryMoveEnemy(level, enemy, enemyXZ, step);
                enemy.Position = new Vector3(moved.X, enemy.Position.Y, moved.Z);
            }

            if (enemy.Kind == EnemyKind.Boss
                && hasLos
                && dist >= BossRangedMin
                && dist <= BossRangedMax
                && enemy.RangedCooldown <= 0f)
            {
                SpawnBossBolt(enemy, player.EyePosition);
                enemy.RangedCooldown = BossRangedCooldown;
            }

            if (hasLos && dist <= PlayerCombatState.EnemyMeleeRange && enemy.MeleeCooldown <= 0f)
            {
                combat.TakeDamage(PlayerCombatState.EnemyMeleeDamage);
                enemy.MeleeCooldown = PlayerCombatState.EnemyMeleeCooldown;
            }
        }

        UpdateBolts(level, player, combat, dt);

        var wantsFire = ctx.IsMousePressed(MouseButton.Left)
            || ctx.IsMouseDown(MouseButton.Left)
            || ctx.IsKeyPressed(KeyboardKey.LeftControl)
            || ctx.IsKeyDown(KeyboardKey.LeftControl);

        if (!wantsFire || !combat.TryConsumeShot())
            return;

        if (TryHit(level, player, combat))
            combat.RegisterHit();
    }

    private void SpawnBossBolt(Enemy boss, Vector3 target)
    {
        var origin = boss.Position + new Vector3(0f, 0.5f, 0f);
        var dir = target - origin;
        if (dir.LengthSquared() < 1e-8f)
            return;

        dir = Vector3.Normalize(dir);
        _bolts.Add(new BossBolt
        {
            Position = origin,
            Velocity = dir * BossBoltSpeed,
        });
    }

    private void UpdateBolts(LevelMap level, PlayerController player, PlayerCombatState combat, float dt)
    {
        for (var i = _bolts.Count - 1; i >= 0; i--)
        {
            var bolt = _bolts[i];
            bolt.TimeToLive -= dt;
            if (bolt.TimeToLive <= 0f)
            {
                _bolts.RemoveAt(i);
                continue;
            }

            var prev = bolt.Position;
            bolt.Position += bolt.Velocity * dt;

            var prevXZ = new Vector3(prev.X, 0f, prev.Z);
            var posXZ = new Vector3(bolt.Position.X, 0f, bolt.Position.Z);
            var deltaXZ = posXZ - prevXZ;
            if (deltaXZ.LengthSquared() > 1e-8f)
            {
                var dirXZ = Vector3.Normalize(deltaXZ);
                var stepDist = deltaXZ.Length();
                if (PlanarOccupancy.TryRaycastWall(
                        level.Walls,
                        prevXZ,
                        dirXZ,
                        stepDist,
                        LevelMap.CellSize,
                        out _))
                {
                    _bolts.RemoveAt(i);
                    continue;
                }
            }

            var playerPos = player.Camera.Position;
            if (Vector3.Distance(bolt.Position, playerPos) < 0.9f)
            {
                combat.TakeDamage(BossBoltDamage);
                _bolts.RemoveAt(i);
            }
        }
    }

    private Vector3 TryMoveEnemy(
        LevelMap level,
        Enemy self,
        Vector3 position, Vector3 delta)
    {
        if (delta.LengthSquared() < 1e-12f)
            return ResolveEnemyPosition(level, self, position);

        var maxStep = self.MoveRadius * 0.5f;
        var remaining = delta;
        var pos = position;
        while (remaining.LengthSquared() > 1e-12f)
        {
            var step = remaining;
            if (step.Length() > maxStep)
                step = Vector3.Normalize(step) * maxStep;

            var next = PlanarOccupancy.TryMove(level.Walls, pos, step, self.MoveRadius, LevelMap.CellSize);
            if (!OverlapsOtherEnemy(self, next, self.MoveRadius))
                pos = next;

            remaining -= step;
        }

        return ResolveEnemyPosition(level, self, pos);
    }

    private static Vector3 ResolveEnemyPosition(LevelMap level, Enemy self, Vector3 position) =>
        PlanarOccupancy.PushOutOfWalls(level.Walls, position, self.MoveRadius, LevelMap.CellSize);

    private bool OverlapsOtherEnemy(Enemy self, Vector3 position, float radius)
    {
        var minDist = radius * 2f;
        var minDistSq = minDist * minDist;

        foreach (var other in _enemies)
        {
            if (!other.Alive || ReferenceEquals(other, self))
                continue;

            var otherXZ = new Vector3(other.Position.X, 0f, other.Position.Z);
            if (Vector3.DistanceSquared(position, otherXZ) < minDistSq)
                return true;
        }

        return false;
    }

    public void Draw(RayGameContext ctx, RayCamera camera)
    {
        foreach (var enemy in _enemies.OrderByDescending(e => e.Position.Z))
        {
            if (!enemy.Alive)
                continue;

            var tex = _sprites[enemy.SpriteIndex % _sprites.Length];
            var size = new Vector2(enemy.BillboardSize, enemy.BillboardSize);
            if (!tex.IsValid)
            {
                var glow = enemy.Kind == EnemyKind.Boss ? 1.1f : 0.7f;
                var color = enemy.Kind == EnemyKind.Boss
                    ? Color.FromArgb(255, 180, 60, 220)
                    : Color.FromArgb(255, 200, 60, 60);
                ctx.DrawGlowSphere(enemy.Position, glow, color);
                continue;
            }

            var source = new RectangleF(0, 0, tex.Width, tex.Height);
            ctx.DrawBillboardPro(camera, tex, source, enemy.Position, size, Color.White);
        }
    }

    public void DrawBolts(RayGameContext ctx)
    {
        foreach (var bolt in _bolts)
        {
            var end = bolt.Position + Vector3.Normalize(bolt.Velocity) * 0.6f;
            ctx.DrawBolt(bolt.Position, end, BossBoltColor);
        }
    }

    public int CountAlive() => _enemies.Count(e => e.Alive);

    public int CountTotal() => _enemies.Count;

    private bool TryHit(LevelMap level, PlayerController player, PlayerCombatState combat)
    {
        var ray = player.GetLookRay();
        var originXZ = new Vector3(ray.Origin.X, 0f, ray.Origin.Z);
        var dirXZ = new Vector3(ray.Direction.X, 0f, ray.Direction.Z);

        var maxRange = MaxShootRange;
        if (PlanarOccupancy.TryRaycastWall(
                level.Walls,
                originXZ,
                dirXZ,
                maxRange,
                LevelMap.CellSize,
                out var wallHit))
            maxRange = wallHit;

        var hitAny = false;
        Enemy? closest = null;
        var closestDist = float.MaxValue;

        foreach (var enemy in _enemies)
        {
            if (!enemy.Alive)
                continue;

            var targetXZ = new Vector3(enemy.Position.X, 0f, enemy.Position.Z);
            if (!PlanarOccupancy.HasLineOfSight(
                    level.Walls,
                    originXZ,
                    targetXZ,
                    LevelMap.CellSize,
                    ShotLosClearance))
                continue;

            if (!PlanarOccupancy.TryRaycastDisc(
                    ray.Origin,
                    ray.Direction,
                    enemy.Position,
                    maxRange,
                    enemy.HitRadius,
                    out var discHit))
                continue;

            var hitDistance = discHit.DistanceAlongRay;

            if (PlanarOccupancy.TryRaycastWall(
                    level.Walls,
                    originXZ,
                    dirXZ,
                    hitDistance,
                    LevelMap.CellSize,
                    out _))
                continue;

            if (hitDistance >= closestDist)
                continue;

            closestDist = hitDistance;
            closest = enemy;
        }

        if (closest is null)
            return false;

        closest.Health -= PlayerCombatState.WeaponDamage;
        hitAny = true;
        combat.LastShotHit = true;
        if (closest.Health <= 0f)
            closest.Alive = false;

        return hitAny;
    }

    private static Texture TryLoad(RayGameContext ctx, string folder, string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", folder, file);
        if (!File.Exists(path))
        {
            Debug.WriteLine($"DoomLite3D: asset not found: {path}");
            return default;
        }

        var tex = ctx.LoadTexture(path);
        return ctx.IsTextureValid(tex) ? tex : default;
    }
}
