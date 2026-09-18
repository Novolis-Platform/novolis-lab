using System.Numerics;
using Novolis.Game.MenuFlows;
using Novolis.Math.Geometry;
using Novolis.Rendering.Backends.TwoD.Silk;
using Novolis.Rendering.TwoD;
using Novolis.Rendering.Presentation;
using TopDownDoom.Art;
using TopDownDoom.Design;

namespace TopDownDoom.Game;

internal sealed class TopDownDoomGame
{
    private const float PlayerRadius = 0.35f;
    private const float CombatZoomFactor = 0.88f;
    private const float ExploreZoomFactor = 1f;

    private readonly TopDownCombatWorld _world = new();
    private readonly DoomLevelState _level = new();
    private readonly CharacterArtLibrary _art = new();
    private readonly LevelFlow _flow = DemoLevelAuthoring.CreateFlow();
    private readonly GameScreenStack _menuFlows = new();
    private SpriteScenePresenter? _sprites;
    private FxPresenter? _fx;
    private bool _playing;
    private bool _paused;
    private float _combatZoom;

    public void Initialize(SilkTwoDGameContext ctx)
    {
        var contentRoot = AppContext.BaseDirectory;
        _art.Initialize(ctx.Scene.Textures, contentRoot);
        _sprites = new SpriteScenePresenter(_art);
        _fx = new FxPresenter(_art);
        BuildLevel(ctx.Scene);
        ctx.SetTitle($"Top-Down Doom — {_art.SourceLabel}");

        ctx.Scene.Camera.WorldUnitsPerPixel = 1f / 30f;
        ctx.Scene.Menus.Push(new TwoDMenuScreen("TOP-DOWN DOOM", [
            new TwoDMenuItem("FIGHT", Tag: "play", OnSelect: () =>
            {
                _playing = true;
                ctx.Scene.Menus.Pop();
                return (object?)"play";
            }),
            new TwoDMenuItem("QUIT", Tag: "quit", OnSelect: () => { Environment.Exit(0); return null; }),
        ]));
    }

    public void Update(SilkTwoDGameContext ctx)
    {
        if (ctx.IsKeyPressed(Key.Escape))
        {
            if (_playing && !_paused)
            {
                _paused = true;
                _ = _menuFlows.PushAsync(new TopDownPauseScreen());
                ctx.Scene.Menus.Push(new TwoDMenuScreen("PAUSED", [
                    new TwoDMenuItem("RESUME", OnSelect: () =>
                    {
                        _paused = false;
                        ctx.Scene.Menus.Pop();
                        return null;
                    }),
                    new TwoDMenuItem("QUIT", OnSelect: () => { Environment.Exit(0); return null; }),
                ]));
            }
        }

        if (!_playing || _paused || ctx.Scene.Menus.IsActive)
        {
            return;
        }

        if (_world.Health <= 0)
        {
            ShowDeathMenu(ctx);
            return;
        }

        if (_world.ExitUnlocked)
        {
            ShowVictoryMenu(ctx);
            return;
        }

        var scene = ctx.Scene;
        var move = ReadMove(ctx);
        var aimWorld = ReadAimWorld(ctx, scene);
        var shoot = ctx.IsMouseButtonDown(MouseButton.Left);
        var dash = ctx.IsKeyPressed(Key.ShiftLeft) || ctx.IsKeyPressed(Key.ShiftRight);
        var interact = ctx.IsKeyPressed(Key.E);

        if (interact)
        {
            CycleWeapon();
        }

        _world.Tick(ctx.DeltaSeconds, move, aimWorld, shoot, dash);
        _world.PlayerPosition = scene.Collision.MoveCircle(
            _world.PlayerPosition,
            new Vector3(_world.PlayerVelocity.X * ctx.DeltaSeconds, 0f, _world.PlayerVelocity.Y * ctx.DeltaSeconds),
            PlayerRadius);

        ApplyLevelScript(scene);

        _combatZoom = float.Lerp(_combatZoom, _world.Monsters.Count > 0 ? CombatZoomFactor : ExploreZoomFactor, 1f - MathF.Exp(-6f * ctx.DeltaSeconds));
        scene.Camera.WorldUnitsPerPixel = (1f / 30f) * _combatZoom;

        var camTarget = _world.PlayerPosition + new Vector3(0f, 0f, 1.2f);
        var t = 1f - MathF.Exp(-10f * ctx.DeltaSeconds);
        var shake = _world.Juice.Shake;
        var shakeOffset = shake > 0.01f
            ? new Vector3(
                (Random.Shared.NextSingle() - 0.5f) * 0.35f * shake,
                0f,
                (Random.Shared.NextSingle() - 0.5f) * 0.35f * shake)
            : Vector3.Zero;
        scene.Camera.Position = Vector3.Lerp(scene.Camera.Position, camTarget + shakeOffset, t);
        scene.Camera.ClearColor = LerpClearColor(
            new Rgba32(14, 10, 18),
            new Rgba32(28, 12, 14),
            MathF.Min(1f, shake * 0.6f + (_world.Monsters.Count > 0 ? 0.15f : 0f)));

        EmitAmbientParticles(ctx.DeltaSeconds);

        scene.Update(ctx.DeltaSeconds);
        _sprites?.Sync(scene, _world, ctx.DeltaSeconds);
        _fx?.Sync(scene, _world.Juice);
        DrawCombatHints(scene);
        DrawHud(scene, ctx);
    }

    private void ApplyLevelScript(TwoDScene scene)
    {
        if (!_level.CorridorLessonTriggered && _world.PlayerPosition.X > 11f && _world.PlayerPosition.Z is > 5f and < 9f)
        {
            _level.CorridorLessonTriggered = true;
            _world.SpawnMonster(MonsterRole.Fodder, 12f, 6f);
            _world.SpawnMonster(MonsterRole.Fodder, 14f, 8f);
            _world.SpawnMonster(MonsterRole.Charger, 16f, 17f);
            _world.Juice.Boom(_world.PlayerPosition + new Vector3(2f, 0f, 0f), 0.6f);
        }

        if (_world.HasBlueKey)
        {
            _level.OpenBlueGate(scene);
        }

        if (_world.ClosetAmbushTriggered)
        {
            _level.OpenClosets(scene);
        }
    }

    private void BuildLevel(TwoDScene scene)
    {
        scene.StaticPolygons.Clear();
        scene.Collision.Clear();
        scene.Sprites.Clear();
        scene.AnimatedSprites.Clear();
        scene.StaticPolygons.RemoveAll(p => p.SortKey is >= 30 and < 200);
        _level.ResetProgress();
        DemoLevelAuthoring.BuildGeometry(scene, _level);
        _level.CaptureCollision(scene);
        DemoLevelAuthoring.SeedEntities(_world);
        _combatZoom = ExploreZoomFactor;
    }

    private void CycleWeapon()
    {
        var list = WeaponCatalog.All;
        var idx = 0;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Name == _world.ActiveWeapon.Name)
            {
                idx = i;
                break;
            }
        }

        _world.ActiveWeapon = list[(idx + 1) % list.Count];
    }

    private static Vector2 ReadMove(SilkTwoDGameContext ctx)
    {
        var x = 0f;
        var z = 0f;
        if (ctx.IsKeyDown(Key.W))
        {
            z += 1f;
        }

        if (ctx.IsKeyDown(Key.S))
        {
            z -= 1f;
        }

        if (ctx.IsKeyDown(Key.A))
        {
            x -= 1f;
        }

        if (ctx.IsKeyDown(Key.D))
        {
            x += 1f;
        }

        return new Vector2(x, z);
    }

    private static Vector2 ReadAimWorld(SilkTwoDGameContext ctx, TwoDScene scene)
    {
        var mouse = ctx.MousePosition;
        var world = scene.Camera.ScreenToWorld(mouse.X, mouse.Y);
        var flat = new Vector2(world.X - scene.Camera.Position.X, world.Z - scene.Camera.Position.Z);
        return flat;
    }

    private void DrawCombatHints(TwoDScene scene)
    {
        scene.StaticPolygons.RemoveAll(p => p.SortKey is >= 30 and < 70);

        foreach (var barrel in _world.Barrels)
        {
            if (!AimNearBarrel(barrel))
            {
                continue;
            }

            DrawRadiusHint(scene, barrel.Position, 2.4f, new Rgba32(255, 120, 60, 80));
        }

        foreach (var monster in _world.Monsters)
        {
            if (monster.Role != MonsterRole.Projectile || monster.FireCooldown >= TimeSpan.FromMilliseconds(400))
            {
                continue;
            }

            DrawWindUpCone(scene, monster.Position, _world.PlayerPosition, new Rgba32(255, 80, 80, 60));
        }
    }

    private bool AimNearBarrel(ExplosiveBarrel barrel)
    {
        var dx = barrel.Position.X - _world.PlayerPosition.X;
        var dz = barrel.Position.Z - _world.PlayerPosition.Z;
        return dx * dx + dz * dz < 6f;
    }

    private void DrawHud(TwoDScene scene, SilkTwoDGameContext ctx)
    {
        scene.Hud.Elements.Clear();
        scene.Hud.AddText("WASD move | Mouse aim | LMB shoot | Shift dash | E swap weapon", 10, 10, 1.8f, new Rgba32(200, 200, 210));
        scene.Hud.AddText(
            $"HP {_world.Health}  AR {_world.Armor}  AMMO {_world.Ammo}  {_world.ActiveWeapon.Name}  KEY {(_world.HasBlueKey ? "BLUE" : "—")}",
            10,
            32,
            2f,
            new Rgba32(220, 220, 235));
        scene.Hud.AddText(_flow.Start.Label + " → blue key → arena → exit", 10, 56, 1.6f, new Rgba32(140, 140, 160));
        scene.Hud.AddText(_art.SourceLabel, 10, 78, 1.4f, new Rgba32(120, 130, 150));
        if (_world.ClosetAmbushTriggered)
        {
            scene.Hud.AddText("MONSTER CLOSET!", 10, 100, 2.2f, new Rgba32(255, 90, 70));
        }

        if (_world.FoundShotgun && _world.ActiveWeapon.Name == "Shotgun")
        {
            scene.Hud.AddText("SHOTGUN!", 10, 122, 2f, new Rgba32(255, 220, 80));
        }
    }

    private void ShowDeathMenu(SilkTwoDGameContext ctx)
    {
        _playing = false;
        ctx.Scene.Menus.Push(new TwoDMenuScreen("YOU DIED", [
            new TwoDMenuItem("RETRY", OnSelect: () =>
            {
                ResetRun(ctx.Scene);
                _playing = true;
                ctx.Scene.Menus.Pop();
                return null;
            }),
            new TwoDMenuItem("QUIT", OnSelect: () => { Environment.Exit(0); return null; }),
        ]));
    }

    private void ShowVictoryMenu(SilkTwoDGameContext ctx)
    {
        _playing = false;
        ctx.Scene.Menus.Push(new TwoDMenuScreen("EXIT OPEN", [
            new TwoDMenuItem("AGAIN", OnSelect: () =>
            {
                ResetRun(ctx.Scene);
                _playing = true;
                ctx.Scene.Menus.Pop();
                return null;
            }),
            new TwoDMenuItem("QUIT", OnSelect: () => { Environment.Exit(0); return null; }),
        ]));
    }

    private void ResetRun(TwoDScene scene)
    {
        _sprites?.Clear(scene);
        _world.Projectiles.Clear();
        _world.Monsters.Clear();
        _world.Pickups.Clear();
        _world.Barrels.Clear();
        _world.Health = 100;
        _world.Armor = 0;
        _world.Ammo = 48;
        _world.HasBlueKey = false;
        _world.ExitUnlocked = false;
        _world.ClosetAmbushTriggered = false;
        _world.FoundShotgun = false;
        _world.ActiveWeapon = WeaponCatalog.Pistol;
        _world.Juice.Clear();
        BuildLevel(scene);
    }

    private static void DrawRadiusHint(TwoDScene scene, Vector3 center, float radius, Rgba32 color)
    {
        var poly = TwoDScenePrimitives.Rectangle(center.X - radius, center.Z - radius, center.X + radius, center.Z + radius);
        scene.StaticPolygons.Add(new TwoDStaticPolygon(poly, color) { DrawFilled = false, DrawOutline = true, SortKey = 30 });
    }

    private static void DrawWindUpCone(TwoDScene scene, Vector3 from, Vector3 to, Rgba32 color)
    {
        var dir = new Vector2(to.X - from.X, to.Z - from.Z);
        if (dir.LengthSquared() < 0.01f)
        {
            return;
        }

        dir = Vector2.Normalize(dir);
        var side = new Vector2(-dir.Y, dir.X) * 0.8f;
        var tip = new Vector3(from.X + dir.X * 1.2f, 0f, from.Z + dir.Y * 1.2f);
        var left = new Vector3(from.X + side.X, 0f, from.Z + side.Y);
        var right = new Vector3(from.X - side.X, 0f, from.Z - side.Y);
        var shape = new Novolis.Math.Topology.Polygon(
        [
            from,
            left,
            tip,
            right,
        ]);
        scene.StaticPolygons.Add(new TwoDStaticPolygon(shape, color) { DrawFilled = true, SortKey = 55 });
    }

    private void EmitAmbientParticles(float dt)
    {
        if (_world.Juice.AmbientCooldown > 0f)
        {
            return;
        }

        _world.Juice.AmbientCooldown = 0.06f;
        var p = _world.PlayerPosition;
        var offset = new Vector3(
            (Random.Shared.NextSingle() - 0.5f) * 8f,
            0f,
            (Random.Shared.NextSingle() - 0.5f) * 8f);
        ParticlePresets.AmbientEmber(_world.Juice.Particles, p + offset);

        foreach (var barrel in _world.Barrels)
        {
            if (Random.Shared.NextSingle() < 0.02f)
            {
                ParticlePresets.BarrelFuse(_world.Juice.Particles, barrel.Position);
            }
        }

        foreach (var pickup in _world.Pickups)
        {
            if (Random.Shared.NextSingle() < 0.12f)
            {
                ParticlePresets.HitSparks(
                    _world.Juice.Particles,
                    pickup.Position,
                    pickup.Kind switch
                    {
                        PickupKind.BlueKey => new Rgba32(100, 160, 255, 180),
                        PickupKind.Health => new Rgba32(80, 255, 100, 180),
                        PickupKind.Exit => new Rgba32(200, 255, 220, 180),
                        _ => new Rgba32(255, 220, 100, 140),
                    },
                    2);
            }
        }
    }

    private static Rgba32 LerpClearColor(Rgba32 a, Rgba32 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Rgba32(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            255);
    }
}

file sealed class TopDownPauseScreen : PauseScreenBase
{
    public override string ScreenId => "top-down-doom-pause";
}
