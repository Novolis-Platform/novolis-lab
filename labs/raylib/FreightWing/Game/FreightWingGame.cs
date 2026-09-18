using System.Drawing;
using System.Numerics;
using Novolis.Lab.SpaceCombat;
using Novolis.Game.MenuFlows;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Raylib.Rendering;
using Novolis.Simulation.SpaceCombat;
using Novolis.Simulation.View;

namespace FreightWing.Game;

internal sealed class FreightWingGame : IDisposable
{
    private static readonly Color SpaceBlack = Color.FromArgb(255, 2, 4, 10);
    private static readonly Color LaserGreen = Color.FromArgb(255, 80, 255, 120);
    private static readonly Color EnemyLaser = Color.FromArgb(255, 255, 70, 50);
    private static readonly Color HullGrey = Color.FromArgb(255, 120, 130, 140);
    private static readonly Color HullDark = Color.FromArgb(255, 40, 42, 48);
    private static readonly Color AccentTeal = Color.FromArgb(255, 40, 180, 160);

    private readonly ContentPack _pack;
    private readonly PilotSave _save;
    private readonly GameScreenStack _screens = new();
    private readonly bool _smoke;

    private MissionSession? _session;
    private int _selectedMission;
    private string? _comms;
    private float _commsTimer;
    private bool _chaseCam;
    private MissionPhase _lastPhase;
    private CrewStation _crewStation = CrewStation.Dual;

    public FreightWingGame(ContentPack pack, bool smoke)
    {
        _pack = pack;
        _smoke = smoke;
        _save = PilotSave.Load();
    }

    public void Initialize(RayGameContext ctx)
    {
        ctx.DisableCursor();
        _screens.PushAsync(new MapScreen()).GetAwaiter().GetResult();
        if (_smoke)
        {
            _selectedMission = 0;
            StartMission();
            _screens.PushAsync(new FlightScreen()).GetAwaiter().GetResult();
        }
    }

    public void Update(RayGameContext ctx)
    {
        var screen = _screens.Current?.ScreenId ?? "map";
        switch (screen)
        {
            case "map":
                UpdateMap(ctx);
                break;
            case "briefing":
                UpdateBriefing(ctx);
                break;
            case "flight":
                UpdateFlight(ctx);
                break;
            case "debrief":
                UpdateDebrief(ctx);
                break;
        }
    }

    private void UpdateMap(RayGameContext ctx)
    {
        ctx.Clear(SpaceBlack);
        ctx.HudText("FREIGHTWING — FAMILY CAMPAIGN", 64, 48, 28, AccentTeal);
        if (!_pack.HasPack)
            ctx.HudText("Content pack missing — using proxy craft. Run Xwa.Cli bake-app.", 64, 84, 16, Color.Orange);

        var missions = _pack.Missions.OrderBy(m => m.UnlockIndex).ToList();
        for (var i = 0; i < missions.Count; i++)
        {
            var m = missions[i];
            var (title, _) = FictionTable.MissionCopy(m.UnlockIndex);
            var locked = m.UnlockIndex > _save.UnlockedThrough;
            var selected = i == _selectedMission;
            var color = locked ? Color.Gray : selected ? Color.Yellow : Color.White;
            var mark = locked ? "[LOCK]" : selected ? ">" : " ";
            ctx.HudText($"{mark} {m.UnlockIndex + 1}. {title}", 80, 140 + i * 36, 22, color);
        }

        ctx.HudText("UP/DOWN select  ENTER brief  ESC quit", 64, ctx.Height - 48, 16, Color.LightGray);

        if (ctx.IsKeyPressed(KeyboardKey.Escape))
            return;
        if (ctx.IsKeyPressed(KeyboardKey.Up) || ctx.IsKeyPressed(KeyboardKey.W))
            _selectedMission = Math.Max(0, _selectedMission - 1);
        if (ctx.IsKeyPressed(KeyboardKey.Down) || ctx.IsKeyPressed(KeyboardKey.S))
            _selectedMission = Math.Min(missions.Count - 1, _selectedMission + 1);
        if (ctx.IsKeyPressed(KeyboardKey.Enter) || ctx.IsKeyPressed(KeyboardKey.Space))
        {
            var m = missions[_selectedMission];
            if (m.UnlockIndex <= _save.UnlockedThrough)
            {
                _screens.PushAsync(new BriefingScreen()).GetAwaiter().GetResult();
            }
        }
    }

    private void UpdateBriefing(RayGameContext ctx)
    {
        var missions = _pack.Missions.OrderBy(m => m.UnlockIndex).ToList();
        var m = missions[_selectedMission];
        var (title, brief) = FictionTable.MissionCopy(m.UnlockIndex);

        ctx.Clear(SpaceBlack);
        ctx.HudText("MISSION BRIEFING", 64, 48, 26, AccentTeal);
        ctx.HudText(title, 64, 100, 24, Color.Yellow);
        ctx.HudText(brief, 64, 150, 18, Color.White);
        ctx.HudText($"Loadout: {FictionTable.CraftName("freighter")} + {FictionTable.CraftName("fighter")}", 64, 220, 18, Color.LightGray);
        ctx.HudText($"Hostiles: {m.HostileCount}   Destroy: {m.DestroyRequired}", 64, 250, 18, Color.LightGray);
        ctx.HudText("ENTER launch   ESC back", 64, ctx.Height - 48, 16, Color.LightGray);

        if (ctx.IsKeyPressed(KeyboardKey.Escape))
        {
            _screens.PopAsync().GetAwaiter().GetResult();
            return;
        }

        if (ctx.IsKeyPressed(KeyboardKey.Enter) || ctx.IsKeyPressed(KeyboardKey.Space))
        {
            StartMission();
            _screens.PushAsync(new FlightScreen()).GetAwaiter().GetResult();
        }
    }

    private void StartMission()
    {
        var missions = _pack.Missions.OrderBy(m => m.UnlockIndex).ToList();
        var m = missions[Math.Clamp(_selectedMission, 0, missions.Count - 1)];
        var freighter = _pack.TryGetProfile(m.FreighterCraftId) ?? _pack.ProfileByRole(CraftRole.Freighter);
        var fighter = _pack.TryGetProfile(m.FighterCraftId) ?? _pack.ProfileByRole(CraftRole.Fighter);
        var hostile = _pack.TryGetProfile(m.HostileCraftId) ?? _pack.ProfileByRole(CraftRole.Hostile);
        _session = new MissionSession(new MissionDescriptor
        {
            Id = m.Id,
            FreighterProfile = freighter,
            FighterProfile = fighter,
            HostileProfile = hostile,
            HostileCount = m.HostileCount,
            ProtectSeconds = m.ProtectSeconds,
            DestroyRequired = m.DestroyRequired,
            MaxHostilesAlive = Math.Max(m.HostileCount + 2, 10),
        });
        _session.Begin();
        _session.CrewStation = _crewStation;
        _session.SetCrewControllers(
            pilot: NeuralImitationController.CreatePilot(),
            gunner: NeuralImitationController.CreateGunner(),
            freighterPilot: NeuralImitationController.CreatePilot(neuralBlend: 0.4f));
        _lastPhase = _session.Phase;
        _comms = "Otana bridge — stay on course. Press G to cycle crew station.";
        _commsTimer = 4f;
        ctxDisableNote();
    }

    private static void ctxDisableNote() { }

    private void UpdateFlight(RayGameContext ctx)
    {
        if (_session is null)
            return;

        if (ctx.IsKeyPressed(KeyboardKey.Escape))
        {
            while (_screens.Current?.ScreenId == "flight" || _screens.Current?.ScreenId == "briefing")
                _screens.PopAsync().GetAwaiter().GetResult();
            _session = null;
            return;
        }

        if (ctx.IsKeyPressed(KeyboardKey.C))
            _chaseCam = !_chaseCam;

        if (ctx.IsKeyPressed(KeyboardKey.G))
        {
            _crewStation = _crewStation switch
            {
                CrewStation.Dual => CrewStation.Pilot,
                CrewStation.Pilot => CrewStation.Gunner,
                _ => CrewStation.Dual,
            };
            _session.CrewStation = _crewStation;
            _comms = _crewStation switch
            {
                CrewStation.Pilot => "Crew: you PILOT — AI gunner online.",
                CrewStation.Gunner => "Crew: you GUNNER — AI pilot online.",
                _ => "Crew: DUAL control.",
            };
            _commsTimer = 3.5f;
        }

        var intent = ReadIntent(ctx);
        _session.Tick(intent, ctx.DeltaSeconds);

        if (_session.Phase != _lastPhase)
        {
            _lastPhase = _session.Phase;
            if (_session.Phase == MissionPhase.Fighter)
            {
                _comms = "Bay clear — X-wing away!";
                _commsTimer = 4f;
            }
            else if (_session.Phase is MissionPhase.Complete or MissionPhase.Failed)
            {
                FinishMission();
                return;
            }
        }

        _commsTimer -= ctx.DeltaSeconds;
        if (_commsTimer <= 0)
            _comms = null;

        if (_smoke && _session.Phase == MissionPhase.Freighter && _session.CanTransfer)
        {
            _session.Tick(new FlightIntent { Transfer = true }, 0.016f);
        }

        DrawFlight(ctx);
    }

    private void FinishMission()
    {
        if (_session is null)
            return;
        var win = _session.Phase == MissionPhase.Complete;
        var score = _session.Kills * 100;
        _save.LastScore = score;
        if (win)
        {
            var missions = _pack.Missions.OrderBy(m => m.UnlockIndex).ToList();
            var m = missions[_selectedMission];
            if (m.UnlockIndex >= _save.UnlockedThrough)
                _save.UnlockedThrough = Math.Min(missions.Count - 1, m.UnlockIndex + 1);
        }

        _save.Store();
        _screens.PushAsync(new DebriefScreen()).GetAwaiter().GetResult();
    }

    private void UpdateDebrief(RayGameContext ctx)
    {
        var win = _session?.Phase == MissionPhase.Complete;
        ctx.Clear(SpaceBlack);
        ctx.HudText(win ? "MISSION COMPLETE" : "MISSION FAILED", 64, 80, 32, win ? AccentTeal : Color.OrangeRed);
        ctx.HudText($"Score {_save.LastScore}", 64, 140, 22, Color.White);
        ctx.HudText($"Campaign unlock through mission {_save.UnlockedThrough + 1}", 64, 180, 18, Color.LightGray);
        ctx.HudText("ENTER return to map", 64, ctx.Height - 48, 16, Color.LightGray);

        if (_smoke || ctx.IsKeyPressed(KeyboardKey.Enter) || ctx.IsKeyPressed(KeyboardKey.Space) || ctx.IsKeyPressed(KeyboardKey.Escape))
        {
            while (_screens.Current is not null && _screens.Current.ScreenId != "map")
                _screens.PopAsync().GetAwaiter().GetResult();
            _session = null;
            if (_smoke)
                Environment.Exit(0);
        }
    }

    private void DrawFlight(RayGameContext ctx)
    {
        var session = _session!;
        var player = session.Player;
        ctx.Clear(SpaceBlack);

        var pose = _chaseCam || player.Profile.Role == CraftRole.Freighter
            ? CraftCamera.ChaseAft(player.Position, player.Forward, player.Roll)
            : CraftCamera.Cockpit(player.Position, player.Forward, player.Roll);

        var camera = Novolis.Raylib.Rendering.Camera.Perspective(
            pose.Position, pose.Target, pose.Up, pose.FieldOfViewDegrees);
        ctx.BeginWorld(camera);

        // Star dust
        for (var i = 0; i < 80; i++)
        {
            var seed = i * 9973;
            var p = player.Position + new Vector3(
                (seed % 100) - 50,
                ((seed / 100) % 60) - 30,
                ((seed / 6000) % 120) - 60);
            ctx.DrawGlowSphere(p, 0.08f, Color.FromArgb(180, 200, 220, 255));
        }

        var freighterMesh = _pack.TryGetMesh(session.Freighter.Profile.MeshId);
        var fighterMesh = _pack.TryGetMesh(session.Fighter.Profile.MeshId);
        var hostileMesh = _pack.TryGetMesh(session.Hostiles[0].Profile.MeshId);

        if (session.Freighter.Active)
            CraftMeshDraw.DrawCraft(ctx, session.Freighter, freighterMesh, HullGrey, AccentTeal);
        if (session.Phase == MissionPhase.Fighter && session.Fighter.Active && !ReferenceEquals(player, session.Fighter))
            CraftMeshDraw.DrawCraft(ctx, session.Fighter, fighterMesh, HullGrey, Color.LimeGreen);

        foreach (var h in session.Hostiles)
            CraftMeshDraw.DrawCraft(ctx, h, hostileMesh, HullDark, Color.DarkRed);

        foreach (var bolt in session.PlayerBolts)
        {
            if (!bolt.Active)
                continue;
            ctx.DrawLaserBolt(bolt.Position, bolt.Position + bolt.Velocity * 0.08f, LaserGreen);
        }

        foreach (var bolt in session.EnemyBolts)
        {
            if (!bolt.Active)
                continue;
            ctx.DrawLaserBolt(bolt.Position, bolt.Position + bolt.Velocity * 0.07f, EnemyLaser);
        }

        ctx.EndWorld();

        var craftName = FictionTable.CraftName(player.Profile.Role.ToString());
        var objective = session.Phase switch
        {
            MissionPhase.Freighter when session.CanTransfer => "Transfer armed — press T",
            MissionPhase.Freighter => $"Escort window {session.ProtectRemaining:0}s",
            MissionPhase.Fighter => $"Protect Otana — destroy {session.Descriptor.DestroyRequired} (have {session.Kills})",
            _ => "",
        };
        FreightWingHud.Draw(ctx, session, craftName, objective, _comms, _crewStation);
    }

    private static FlightIntent ReadIntent(RayGameContext ctx)
    {
        var mouse = ctx.MouseDelta;
        return new FlightIntent
        {
            YawDelta = mouse.X * 0.0022f,
            PitchDelta = -mouse.Y * 0.0022f,
            RollLeft = ctx.IsKeyDown(KeyboardKey.A) ? 1f : 0f,
            RollRight = ctx.IsKeyDown(KeyboardKey.D) ? 1f : 0f,
            ThrottleUp = ctx.IsKeyDown(KeyboardKey.W) ? 1f : 0f,
            ThrottleDown = ctx.IsKeyDown(KeyboardKey.S) ? 1f : 0f,
            Fire = ctx.IsKeyDown(KeyboardKey.Space) || ctx.IsMouseDown(MouseButton.Left),
            Transfer = ctx.IsKeyPressed(KeyboardKey.T),
        };
    }

    public void Dispose() => _pack.Dispose();
}
