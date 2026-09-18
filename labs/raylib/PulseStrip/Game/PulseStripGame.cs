namespace PulseStrip.Game;

using System.Drawing;
using System.Numerics;
using Novolis.Audio;
using Novolis.Game.MenuFlows;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Simulation.Racing.Tracks;
using PulseStrip.Audio;
using PulseStrip.Core;
using PulseStrip.Core.Ai;

internal sealed class PulseStripGame : IDisposable
{
    private readonly string _contentDir;
    private readonly IAudioEngine _audio;
    private readonly PulseStripSfx _sfx;
    private readonly GameScreenStack _screens = new();

    private int _circuitIndex;
    private HoverRaceSimulation? _sim;
    private PlayerHoverController? _player;
    private TrackRibbonMesh? _ribbon;
    private readonly List<VfxSpark> _sparks = [];
    private float _resultsTimer;
    private string _status = "";

    private readonly record struct VfxSpark(Vector3 Pos, Vector3 Vel, float Life, Color Color);

    public PulseStripGame(string contentDir, IAudioEngine audio, bool smoke)
    {
        _ = smoke;
        _contentDir = contentDir;
        _audio = audio;
        _sfx = new PulseStripSfx(audio, contentDir);
    }

    public void Initialize(RayGameContext ctx)
    {
        _sfx.EnsureGenerated();
        _ = ShipMeshCache.Player; // warm FBX load
        _screens.PushAsync(new TitleScreen()).GetAwaiter().GetResult();
        _sfx.Play("blip");
    }

    public void Update(RayGameContext ctx)
    {
        var screen = _screens.Current?.ScreenId ?? "title";
        switch (screen)
        {
            case "title":
                UpdateTitle(ctx);
                break;
            case "circuit":
                UpdateCircuit(ctx);
                break;
            case "race":
                UpdateRace(ctx);
                break;
            case "results":
                UpdateResults(ctx);
                break;
        }
    }

    private void UpdateTitle(RayGameContext ctx)
    {
        ctx.Clear(PulseStripLook.Void);
        ctx.HudRect(0, ctx.Height / 2, ctx.Width, ctx.Height / 2, Color.FromArgb(255, 10, 2, 24));
        ctx.HudText("PULSESTRIP", 72, 110, 64, PulseStripLook.HudCyan);
        ctx.HudText("ANTI-GRAVITY COMBAT RACING", 76, 190, 22, PulseStripLook.RailMagenta);
        ctx.HudText("ENTER — SELECT CIRCUIT", 76, 300, 20, Color.White);
        ctx.HudText("W/S THROTTLE   A/D STEER   SHIFT BOOST   SPACE FIRE", 76, 340, 16, PulseStripLook.HudDim);
        ctx.HudText("Ship meshes: Synert/WipeoutClone (MIT)", 76, ctx.Height - 72, 14, PulseStripLook.HudDim);
        ctx.HudText("ESC — QUIT", 76, ctx.Height - 48, 16, Color.Gray);

        if (ctx.IsKeyPressed(KeyboardKey.Enter) || ctx.IsKeyPressed(KeyboardKey.Space))
        {
            _sfx.Play("blip");
            _screens.PushAsync(new CircuitScreen()).GetAwaiter().GetResult();
        }

        if (ctx.IsKeyPressed(KeyboardKey.Escape))
            Environment.Exit(0);
    }

    private void UpdateCircuit(RayGameContext ctx)
    {
        ctx.Clear(PulseStripLook.Void);
        ctx.HudText("SELECT CIRCUIT", 72, 80, 36, PulseStripLook.HudAmber);
        for (var i = 0; i < PulseStripCircuits.All.Count; i++)
        {
            var selected = i == _circuitIndex;
            ctx.HudText(
                $"{(selected ? ">" : " ")} {PulseStripCircuits.DisplayName(i).ToUpperInvariant()}",
                90, 170 + i * 48, 26,
                selected ? PulseStripLook.HudCyan : Color.White);
        }

        ctx.HudText("UP/DOWN   ENTER RACE   ESC BACK", 72, ctx.Height - 48, 16, PulseStripLook.HudDim);

        if (ctx.IsKeyPressed(KeyboardKey.Up) || ctx.IsKeyPressed(KeyboardKey.W))
            _circuitIndex = Math.Max(0, _circuitIndex - 1);
        if (ctx.IsKeyPressed(KeyboardKey.Down) || ctx.IsKeyPressed(KeyboardKey.S))
            _circuitIndex = Math.Min(PulseStripCircuits.All.Count - 1, _circuitIndex + 1);
        if (ctx.IsKeyPressed(KeyboardKey.Escape))
        {
            _screens.PopAsync().GetAwaiter().GetResult();
            return;
        }

        if (ctx.IsKeyPressed(KeyboardKey.Enter) || ctx.IsKeyPressed(KeyboardKey.Space))
        {
            _sfx.Play("blip");
            StartRace();
            _screens.PushAsync(new RaceScreen()).GetAwaiter().GetResult();
        }
    }

    private void StartRace()
    {
        var def = PulseStripCircuits.ByIndex(_circuitIndex);
        var track = new PulseStripTrackBuilder().Build(def);
        _ribbon = TrackRibbonMesh.Build(track);
        var brainsDir = Path.Combine(_contentDir, "brains");
        IReadOnlyList<Novolis.MachineLearning.Neural.INeuralNetwork> brains;
        try
        {
            brains = BrainStore.LoadOrTrain(brainsDir, count: 3, trainTrack: BuiltInTracks.MicroCircle);
        }
        catch
        {
            brains = [];
        }

        _player = new PlayerHoverController("YOU");
        var controllers = new List<IHoverController> { _player };
        for (var i = 0; i < 3; i++)
        {
            var callsign = $"AG-{i + 1:00}";
            if (i < brains.Count)
                controllers.Add(new NamedHoverController(callsign, new NeuralHoverController(brains[i])));
            else
                controllers.Add(new FullThrottleHoverController(callsign));
        }

        _sim = new HoverRaceSimulation(track, controllers, targetLaps: 3);
        _sim.WeaponFired += _ => _sfx.Play("fire");
        _sim.WeaponHit += (_, victim) =>
        {
            _sfx.Play("hit");
            SpawnSparks(victim.Position, PulseStripLook.Plasma, 6);
        };
        _sim.PickupCollected += (_, _) => _sfx.Play("pickup");
        _sim.LapCompleted += c =>
        {
            if (c.Id == 0)
                _sfx.Play("lap");
        };
        _sparks.Clear();
        _status = PulseStripCircuits.DisplayName(_circuitIndex);
    }

    private void UpdateRace(RayGameContext ctx)
    {
        if (_sim is null || _player is null || _ribbon is null)
            return;

        _player.Current = ReadPlayerInput(ctx);
        _sim.Tick();

        var player = _sim.State.Craft[0];
        if (player.Boosting && _sim.State.Tick % 4 == 0)
            SpawnSparks(player.Position - player.Forward * 1.6f, PulseStripLook.Boost, 1);
        if (player.Boosting && _sim.State.Tick % 18 == 0)
            _sfx.Play("boost");

        TickSparks(ctx.DeltaSeconds);
        DrawRace(ctx);

        if (_sim.State.RaceFinished || player.Finished || player.Crashed)
        {
            _resultsTimer = 0;
            _screens.PushAsync(new ResultsScreen()).GetAwaiter().GetResult();
        }

        if (ctx.IsKeyPressed(KeyboardKey.Escape))
        {
            while (_screens.Current is not null && _screens.Current.ScreenId != "circuit")
                _screens.PopAsync().GetAwaiter().GetResult();
            _sim = null;
            _ribbon = null;
        }
    }

    private void UpdateResults(RayGameContext ctx)
    {
        ctx.Clear(PulseStripLook.Void);
        var craft = _sim?.State.Craft;
        ctx.HudText("RACE COMPLETE", 72, 80, 40, PulseStripLook.HudAmber);
        if (craft is not null)
        {
            foreach (var (c, i) in craft.OrderBy(x => x.Place).Select((c, i) => (c, i)))
            {
                var tag = c.Crashed ? "DNF" : $"P{c.Place}";
                ctx.HudText($"{tag}  {c.Name}", 90, 170 + i * 40, 24, c.Id == 0 ? PulseStripLook.HudCyan : Color.White);
            }
        }

        _resultsTimer += ctx.DeltaSeconds;
        ctx.HudText(_resultsTimer > 1.2f ? "ENTER — CIRCUITS    ESC — TITLE" : "…", 72, ctx.Height - 48, 18, PulseStripLook.HudDim);

        if (_resultsTimer > 1.2f && (ctx.IsKeyPressed(KeyboardKey.Enter) || ctx.IsKeyPressed(KeyboardKey.Space)))
        {
            while (_screens.Current is not null && _screens.Current.ScreenId != "circuit")
                _screens.PopAsync().GetAwaiter().GetResult();
            _sim = null;
            _ribbon = null;
            _sfx.Play("blip");
        }

        if (ctx.IsKeyPressed(KeyboardKey.Escape))
        {
            while (_screens.Current is not null && _screens.Current.ScreenId != "title")
                _screens.PopAsync().GetAwaiter().GetResult();
            _sim = null;
            _ribbon = null;
        }
    }

    private void DrawRace(RayGameContext ctx)
    {
        var sim = _sim!;
        var ribbon = _ribbon!;
        var player = sim.State.Craft[0];
        ctx.Clear(PulseStripLook.Void);

        var camera = PulseStripLook.ChaseCamera(player);
        ctx.BeginWorld(camera);

        PulseStripLook.DrawAtmosphere(ctx, player.Position);
        PulseStripLook.DrawCircuit(ctx, sim.Track, ribbon, sim.State.Tick);
        PulseStripLook.DrawSpeedStreaks(player);

        foreach (var pad in sim.State.Pickups)
        {
            if (pad.Available)
                PulseStripLook.DrawPickup(pad, sim.State.Tick);
        }

        foreach (var c in sim.State.Craft)
        {
            if (!c.Crashed)
                PulseStripLook.DrawShip(ctx, c, player: c.Id == 0);
        }

        foreach (var p in sim.State.Projectiles)
        {
            if (!p.Active)
                continue;
            var dir = Vector3.Normalize(p.Velocity);
            var tip = p.Position + dir * 2.8f;
            PulseStripNativeDraw.Triangle(
                tip,
                p.Position + Vector3.UnitY * 0.12f,
                p.Position - Vector3.UnitY * 0.12f,
                PulseStripLook.Plasma);
            ctx.DrawLaserBolt(p.Position, tip, PulseStripLook.Plasma);
        }

        foreach (var s in _sparks)
            PulseStripNativeDraw.Triangle(
                s.Pos,
                s.Pos + new Vector3(0.08f, 0.08f, 0),
                s.Pos + new Vector3(-0.08f, 0.08f, 0),
                s.Color);

        ctx.EndWorld();
        PulseStripLook.DrawRaceHud(ctx, sim, player, _status);
    }

    private static HoverControlDecision ReadPlayerInput(RayGameContext ctx)
    {
        var steer = 0.0;
        if (ctx.IsKeyDown(KeyboardKey.A))
            steer -= 1;
        if (ctx.IsKeyDown(KeyboardKey.D))
            steer += 1;

        var throttle = 0.0;
        var brake = 0.0;
        if (ctx.IsKeyDown(KeyboardKey.W) || ctx.IsKeyDown(KeyboardKey.Up))
            throttle = 1;
        if (ctx.IsKeyDown(KeyboardKey.S) || ctx.IsKeyDown(KeyboardKey.Down))
            brake = 1;
        if (throttle < 0.1 && brake < 0.1)
            throttle = 0.55;

        var boost = ctx.IsKeyDown(KeyboardKey.LeftShift) ? 1.0 : 0.0;
        var fire = ctx.IsKeyPressed(KeyboardKey.Space);
        return new HoverControlDecision(steer, throttle, brake, boost, fire);
    }

    private void SpawnSparks(Vector3 origin, Color color, int count)
    {
        var rng = Random.Shared;
        for (var i = 0; i < count; i++)
        {
            var vel = new Vector3(
                (float)(rng.NextDouble() * 2 - 1),
                (float)(rng.NextDouble() * 1.5),
                (float)(rng.NextDouble() * 2 - 1));
            _sparks.Add(new VfxSpark(origin, vel * 3f, 0.18f + (float)rng.NextDouble() * 0.12f, color));
        }

        while (_sparks.Count > 40)
            _sparks.RemoveAt(0);
    }

    private void TickSparks(float dt)
    {
        for (var i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            s = s with { Pos = s.Pos + s.Vel * dt, Life = s.Life - dt, Vel = s.Vel + Vector3.UnitY * -3f * dt };
            if (s.Life <= 0)
                _sparks.RemoveAt(i);
            else
                _sparks[i] = s;
        }
    }

    public void Dispose()
    {
        _sfx.Dispose();
        _audio.Dispose();
    }
}

internal sealed class NamedHoverController(string name, IHoverController inner) : IHoverController
{
    public string Name { get; } = name;
    public HoverControlDecision Decide(in HoverObservation observation) => inner.Decide(in observation);
}
