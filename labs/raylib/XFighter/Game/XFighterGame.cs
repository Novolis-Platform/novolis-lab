using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Raylib.Rendering;
using XFighter.Game.Audio;

namespace XFighter.Game;

internal sealed class XFighterGame
{
    private const int MaxActiveEnemies = 4;

    private static readonly Color SpaceBlack = Color.FromArgb(255, 2, 4, 10);
    private static readonly Color LaserGreen = Color.FromArgb(255, 80, 255, 120);
    private static readonly Color LaserCore = Color.FromArgb(255, 200, 255, 220);
    private static readonly Color EnemyLaser = Color.FromArgb(255, 255, 70, 50);
    private static readonly Color ExplosionCore = Color.FromArgb(255, 255, 200, 100);

    private readonly Random _rng = new(42);
    private readonly PlayerFlight _player = new();
    private readonly Starfield _starfield;
    private readonly BattleSpace _battleSpace;
    private readonly CockpitHud _hud = new();
    private readonly WingmanChatter _chatter;
    private readonly XFighterSoundscape _audio = new();
    private readonly XFighterVoice _voice = XFighterVoice.TryCreate();
    private readonly HFighter[] _enemies;
    private readonly LaserBolt[] _bolts;
    private readonly LaserBolt[] _enemyBolts;
    private readonly Explosion[] _explosions;

    private float _fireCooldown;
    private float _spawnTimer;
    private int _score;
    private float _shield = 1f;
    private int _killsThisFrame;
    private float _prevShield = 1f;

    public XFighterGame()
    {
        _starfield = new Starfield(520, _rng);
        _battleSpace = new BattleSpace(_rng);
        _chatter = new WingmanChatter(_rng);
        _enemies = Enumerable.Range(0, 10).Select(_ => new HFighter()).ToArray();
        _bolts = Enumerable.Range(0, 48).Select(_ => new LaserBolt()).ToArray();
        _enemyBolts = Enumerable.Range(0, 32).Select(_ => new LaserBolt { FromPlayer = false }).ToArray();
        _explosions = Enumerable.Range(0, 16).Select(_ => new Explosion()).ToArray();
    }

    public void Initialize(RayGameContext ctx)
    {
        _hud.Initialize(ctx);
        _audio.Start();
        ctx.DisableCursor();
        SpawnWave();
        AnnounceComms(_chatter.AnnounceWave());
    }

    public void Update(RayGameContext ctx)
    {
        if (ctx.IsKeyPressed(KeyboardKey.M))
            _audio.Enabled = !_audio.Enabled;

        if (ctx.IsKeyPressed(KeyboardKey.Escape))
            return;

        if (ctx.IsKeyPressed(KeyboardKey.R))
            ResetMission();

        _killsThisFrame = 0;
        _player.Update(ctx);
        _audio.UpdateEngine(_player.Throttle01);

        UpdateCombat(ctx);
        UpdateEnemies(ctx);
        TrySpawn(ctx);

        var lockTarget = TargetingComputer.FindLockTarget(_enemies, _player.Position, _player.Forward);
        var commsLine = _chatter.Update(ctx.DeltaSeconds, CountActiveEnemies(), _shield, _killsThisFrame);
        if (commsLine is not null)
            AnnounceComms(commsLine);

        ctx.Clear(SpaceBlack);
        var camera = _player.BuildCamera();
        ctx.BeginWorld(camera);
        _battleSpace.Draw(ctx, _player.Position);
        _starfield.Draw(ctx, _player.Position, _player.Speed, _player.Forward);
        DrawWorld(ctx, lockTarget);
        ctx.EndWorld();
        _hud.Draw(ctx, _player, _score, CountActiveEnemies(), _shield, lockTarget, _chatter);
        _prevShield = _shield;
    }

    private void AnnounceComms(string line)
    {
        _audio.PlayRadio();
        if (_voice.IsAvailable)
            _voice.SpeakComms(line);
    }

    private void ResetMission()
    {
        _score = 0;
        _shield = 1f;
        _prevShield = 1f;
        _player.Position = Vector3.Zero;
        _player.Speed = 22f;
        _player.Roll = 0;
        foreach (var e in _enemies)
            e.Active = false;
        foreach (var b in _bolts)
            b.Active = false;
        foreach (var b in _enemyBolts)
            b.Active = false;
        _chatter.Reset();
        SpawnWave();
        AnnounceComms(_chatter.AnnounceWave());
    }

    private void DrawWorld(RayGameContext ctx, HFighter? lockTarget)
    {
        foreach (var e in _enemies)
            e.Draw(ctx);

        if (lockTarget is { Active: true })
            TargetingComputer.DrawLockBrackets(ctx, lockTarget);

        foreach (var bolt in _bolts)
        {
            if (!bolt.Active)
                continue;
            var end = bolt.Position + bolt.Velocity * 0.08f;
            ctx.DrawLaserBolt(bolt.Position, end, LaserGreen);
            ctx.DrawGlowSphere(bolt.Position, 0.08f, LaserCore);
        }

        foreach (var bolt in _enemyBolts)
        {
            if (!bolt.Active)
                continue;
            var end = bolt.Position + bolt.Velocity * 0.07f;
            ctx.DrawLaserBolt(bolt.Position, end, EnemyLaser);
        }

        foreach (var ex in _explosions)
        {
            if (!ex.Active)
                continue;
            var t = 1f - ex.Life / ex.MaxLife;
            var r = 0.5f + t * 4f;
            ctx.DrawGlowSphere(ex.Position, r, ExplosionCore);
            ctx.DrawGlowSphereWires(ex.Position, r * 1.2f, LaserGreen);
        }
    }

    private void UpdateCombat(RayGameContext ctx)
    {
        var dt = ctx.DeltaSeconds;
        _fireCooldown = Math.Max(0, _fireCooldown - dt);

        var firing = ctx.IsKeyDown(KeyboardKey.Space) || ctx.IsMouseDown(MouseButton.Left);
        if (firing && _fireCooldown <= 0)
        {
            _fireCooldown = 0.14f;
            if (FireBolt())
                _audio.PlayLaser();
        }

        UpdateBolts(_bolts, dt, playerBolts: true);
        UpdateBolts(_enemyBolts, dt, playerBolts: false);

        ResolvePlayerHits();
        ResolveEnemyHits();

        foreach (var ex in _explosions)
        {
            if (!ex.Active)
                continue;
            ex.Life -= dt;
            if (ex.Life <= 0)
                ex.Active = false;
        }

        if (_shield < _prevShield - 0.01f)
            _audio.PlayShieldHit();
    }

    private void UpdateBolts(LaserBolt[] bolts, float dt, bool playerBolts)
    {
        foreach (var bolt in bolts)
        {
            if (!bolt.Active)
                continue;
            bolt.Position += bolt.Velocity * dt;
            bolt.Life -= dt;
            if (bolt.Life <= 0)
                bolt.Active = false;
            else if (playerBolts && Vector3.Distance(bolt.Position, _player.Position) > 200f)
                bolt.Active = false;
            else if (!playerBolts && Vector3.Distance(bolt.Position, _player.Position) > 90f)
                bolt.Active = false;
        }
    }

    private void ResolvePlayerHits()
    {
        foreach (var bolt in _bolts)
        {
            if (!bolt.Active)
                continue;
            var prev = bolt.Position - bolt.Velocity * 0.016f;
            foreach (var enemy in _enemies)
            {
                if (!enemy.Active)
                    continue;
                if (!CombatSystem.SegmentHitsSphere(prev, bolt.Position, enemy.Position, enemy.HitRadius))
                    continue;

                bolt.Active = false;
                enemy.Health -= 0.55f;
                if (enemy.Health <= 0)
                {
                    enemy.Active = false;
                    _score += 100;
                    _killsThisFrame++;
                    SpawnExplosion(enemy.Position);
                    _audio.PlayExplosion();
                }

                break;
            }
        }
    }

    private void ResolveEnemyHits()
    {
        foreach (var bolt in _enemyBolts)
        {
            if (!bolt.Active)
                continue;
            if (!CombatSystem.SegmentHitsSphere(
                    bolt.Position - bolt.Velocity * 0.016f,
                    bolt.Position,
                    _player.Position,
                    2.5f))
                continue;

            bolt.Active = false;
            _shield -= 0.1f;
            if (_shield < 0)
                _shield = 0;
        }
    }

    private void UpdateEnemies(RayGameContext ctx)
    {
        var dt = ctx.DeltaSeconds;
        var active = CountActiveEnemies();
        foreach (var enemy in _enemies)
        {
            enemy.Update(dt, _player.Position, _enemies);
            if (!enemy.TryFire(_player.Position, active))
                continue;

            enemy.GetBoltVelocity(_player.Position, out var origin, out var velocity);
            foreach (var bolt in _enemyBolts)
            {
                if (bolt.Active)
                    continue;
                bolt.Active = true;
                bolt.Life = 2f;
                bolt.Position = origin;
                bolt.Velocity = velocity;
                _audio.PlayEnemyBolt();
                break;
            }
        }
    }

    private void TrySpawn(RayGameContext ctx)
    {
        _spawnTimer -= ctx.DeltaSeconds;
        if (_spawnTimer > 0 || CountActiveEnemies() >= MaxActiveEnemies)
            return;

        _spawnTimer = 5f;
        foreach (var enemy in _enemies)
        {
            if (enemy.Active)
                continue;
            enemy.Spawn(_player.Position, _player.Forward, _rng);
            break;
        }
    }

    private void SpawnWave()
    {
        var spawned = 0;
        foreach (var enemy in _enemies)
        {
            if (spawned >= 2)
                break;
            enemy.Spawn(_player.Position, _player.Forward, _rng);
            spawned++;
        }

        _spawnTimer = 3f;
    }

    private bool FireBolt()
    {
        foreach (var bolt in _bolts)
        {
            if (bolt.Active)
                continue;
            bolt.Active = true;
            bolt.FromPlayer = true;
            bolt.Life = 2.5f;
            bolt.Position = _player.Position + _player.Forward * 2f;
            bolt.Velocity = _player.Forward * 120f;
            return true;
        }

        return false;
    }

    private void SpawnExplosion(Vector3 pos)
    {
        foreach (var ex in _explosions)
        {
            if (ex.Active)
                continue;
            ex.Active = true;
            ex.Position = pos;
            ex.MaxLife = 0.55f;
            ex.Life = ex.MaxLife;
            return;
        }
    }

    private int CountActiveEnemies()
    {
        var n = 0;
        foreach (var e in _enemies)
            if (e.Active)
                n++;
        return n;
    }
}
