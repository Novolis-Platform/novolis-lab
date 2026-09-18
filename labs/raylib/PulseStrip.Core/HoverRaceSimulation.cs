namespace PulseStrip.Core;

using System.Numerics;
using Novolis.Simulation.Racing.Cars;
using Novolis.Simulation.Racing.Progress;
using Novolis.Simulation.Racing.Rewards;
using Novolis.Simulation.Racing.Sensors;
using Novolis.Simulation.Racing.Tracks;

/// <summary>
/// Anti-grav race loop on a <see cref="RaceTrack"/> spline raster:
/// boost, weapons, pickups, altitude band, wall scrape damage.
/// </summary>
public sealed class HoverRaceSimulation
{
    public const float HoverHeight = 1.35f;
    public const int SensorInputSize = 10;
    public const int ControlOutputSize = 5;

    private const double DeltaTime = 1.0 / 60.0;
    private const double MaxSpeed = 48.0;       // 3× prior 16
    private const double BoostMaxSpeed = 78.0;  // 3× prior 26
    private const double Acceleration = 33.0;   // 3× prior 11
    private const double MaxTurnRate = 5.2;     // 2× so steering keeps up with 3× speed
    private const double BoostDrainPerSecond = 0.35;
    private const double BoostRegenPerSecond = 0.12;
    private const float ProjectileSpeed = 126f; // 3× prior 42
    private const float PickupRadius = 2.2f;
    private const float ProjectileHitRadius = 1.8f;

    private readonly ICarSensorModel _sensors = new DefaultCarSensorModel();
    private readonly ILapScorer _laps = new LapScorer();
    private readonly ITrackProgressResolver _progress = new TrackProgressResolver();
    private readonly IRewardModel? _rewardModel;
    private readonly double[]? _rewardAccum;
    private readonly MobiusTrackFrames.SurfaceFrame[] _frames;

    public RaceTrack Track { get; }
    public IReadOnlyList<IHoverController> Controllers { get; }
    public HoverRaceWorldState State { get; private set; }
    public IReadOnlyList<MobiusTrackFrames.SurfaceFrame> SurfaceFrames => _frames;

    /// <summary>Raised when a craft fires a weapon (for SFX/VFX hooks).</summary>
    public event Action<HoverCraftState>? WeaponFired;

    /// <summary>Raised when a projectile hits a craft.</summary>
    public event Action<HoverCraftState, HoverCraftState>? WeaponHit;

    /// <summary>Raised when a craft collects a pickup.</summary>
    public event Action<HoverCraftState, TrackPickup>? PickupCollected;

    /// <summary>Raised when a craft completes a lap.</summary>
    public event Action<HoverCraftState>? LapCompleted;

    public HoverRaceSimulation(
        RaceTrack track,
        IReadOnlyList<IHoverController> controllers,
        int targetLaps = 3,
        IRewardModel? trainingRewardModel = null,
        double[]? trainingRewardAccumulator = null)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(controllers);
        if (controllers.Count == 0)
            throw new ArgumentException("At least one controller is required.", nameof(controllers));

        Track = track;
        Controllers = controllers;
        _rewardModel = trainingRewardModel;
        _rewardAccum = trainingRewardAccumulator;
        _frames = MobiusTrackFrames.Build(track.CenterLineSamples);

        if (trainingRewardModel is not null)
        {
            if (trainingRewardAccumulator is null)
                throw new ArgumentNullException(nameof(trainingRewardAccumulator));
            if (trainingRewardAccumulator.Length != controllers.Count)
                throw new ArgumentException("Reward accumulator length must match controllers.", nameof(trainingRewardAccumulator));
        }

        State = CreateInitialState(targetLaps);
    }

    public void Reset(int? targetLaps = null)
    {
        State = CreateInitialState(targetLaps ?? State.TargetLaps);
    }

    public void Tick()
    {
        State.Tick++;
        var craft = State.Craft;

        for (var i = 0; i < craft.Count; i++)
        {
            var c = craft[i];
            if (c.Crashed || c.Finished)
                continue;

            var before = c.CloneForComparison();
            var carProxy = ToCarState(c);
            var progressBefore = _progress.Resolve(Track, Flat(c.Position), c.Forward);
            var sensors = _sensors.Read(Track, carProxy);
            var standing = GetStanding(c);
            var obs = new HoverObservation(i, c, sensors, progressBefore, standing);
            var decision = Controllers[i].Decide(in obs);

            IntegrateMotion(c, decision);
            ResolveCollisions(c);
            UpdateShield(c);
            CollectPickups(c);

            if (decision.Fire && c.WeaponAmmo > 0)
                FireWeapon(c);

            if (c.Crashed)
            {
                c.Speed = 0;
                AccumulateReward(i, before, c, progressBefore, _progress.Resolve(Track, Flat(c.Position), c.Forward));
                continue;
            }

            var lapCar = ToCarState(c);
            lapCar.PreviousPosition = Flat(before.Position);
            lapCar.Position = Flat(c.Position);
            lapCar.CurrentGateIndex = c.CurrentGateIndex;
            lapCar.CompletedLaps = c.CompletedLaps;
            var lapsBefore = lapCar.CompletedLaps;
            _laps.Update(Track, lapCar);
            c.CurrentGateIndex = lapCar.CurrentGateIndex;
            c.CompletedLaps = lapCar.CompletedLaps;
            if (c.CompletedLaps > lapsBefore)
                LapCompleted?.Invoke(c);

            // Keep continuous arc progress from IntegrateMotion; only re-base lap count.
            var loopT = c.TrackProgress - Math.Floor(c.TrackProgress);
            if (loopT < 0)
                loopT += 1;
            c.TrackProgress = c.CompletedLaps + loopT;
            c.TicksAlive++;

            if (c.CompletedLaps >= State.TargetLaps)
                c.Finished = true;

            var progressAfter = _progress.Resolve(Track, Flat(c.Position), c.Forward);
            AccumulateReward(i, before, c, progressBefore, progressAfter);
        }

        UpdateProjectiles();
        UpdatePickupRespawns();
        UpdatePlaces();

        if (craft.All(x => x.Finished || x.Crashed) || craft.Count(x => x.Finished) > 0 && craft.All(x => x.Finished || x.Crashed || x.Id != craft[0].Id))
        {
            // Race ends when player finishes or everyone is done.
            if (craft[0].Finished || craft.All(x => x.Finished || x.Crashed))
                State.RaceFinished = true;
        }
    }

    private void IntegrateMotion(HoverCraftState c, HoverControlDecision decision)
    {
        c.PreviousPosition = c.Position;

        var boosting = decision.Boost > 0.5 && c.BoostFuel > 0.05;
        c.Boosting = boosting;
        if (boosting)
            c.BoostFuel = Math.Max(0, c.BoostFuel - BoostDrainPerSecond * DeltaTime);
        else
            c.BoostFuel = Math.Min(1.0, c.BoostFuel + BoostRegenPerSecond * DeltaTime);

        var maxSpeed = boosting ? BoostMaxSpeed : MaxSpeed;
        c.Speed += (decision.Throttle - decision.Brake) * Acceleration * DeltaTime;
        if (boosting)
            c.Speed += Acceleration * 0.6 * DeltaTime;
        c.Speed = Math.Clamp(c.Speed, 0, maxSpeed);

        var totalArc = Track.ProgressMap.TotalArcLength;
        if (totalArc < 1 || _frames.Length == 0)
            return;

        // Continuous lap fraction — do not reseat from discrete nearest-sample LoopT
        // (that snapped the craft back to the same point every tick).
        var loopT = c.TrackProgress - Math.Floor(c.TrackProgress);
        if (loopT < 0)
            loopT += 1;

        var frame = MobiusTrackFrames.AtLoopT(_frames, loopT);
        var lateral = Vector3.Dot(c.Position - frame.Position, frame.Right);
        // Steering slides across the ribbon; yaw is mostly rail-locked.
        lateral += (float)(decision.Steering * c.Speed * 0.55 * DeltaTime);
        var half = (float)(Track.Geometry.HalfWidth * 0.95);
        lateral = Math.Clamp(lateral, -half, half);

        loopT += (c.Speed * DeltaTime) / totalArc;
        loopT -= Math.Floor(loopT);

        frame = MobiusTrackFrames.AtLoopT(_frames, loopT);
        var hover = HoverHeight + (boosting ? 0.35f : 0f) + MathF.Sin(State.Tick * 0.08f + c.Id) * 0.05f;
        c.Position = frame.Position + frame.Right * lateral + frame.Up * hover;
        c.Forward = frame.Tangent.LengthSquared() > 1e-8f
            ? Vector3.Normalize(frame.Tangent)
            : c.Forward;
        c.Bank = Math.Clamp(frame.TwistRadians / MathF.PI, -2f, 2f);
        // Keep continuous progress so the next tick doesn't re-snap; lap scorer still owns CompletedLaps.
        c.TrackProgress = Math.Floor(c.TrackProgress) + loopT;
    }

    private void ResolveCollisions(HoverCraftState c)
    {
        if (_frames.Length == 0)
            return;

        var loopT = c.TrackProgress - Math.Floor(c.TrackProgress);
        if (loopT < 0)
            loopT += 1;
        var frame = MobiusTrackFrames.AtLoopT(_frames, loopT);
        var lateral = Vector3.Dot(c.Position - frame.Position, frame.Right);
        var half = Track.Geometry.HalfWidth;
        var wallBand = Math.Max(1.0, half * 0.12);

        if (Math.Abs(lateral) <= half)
            return;

        c.Health -= c.ShieldActive ? 4.0 : 12.0;
        c.Speed *= 0.35;
        var push = (float)(-Math.Sign(lateral) * Math.Max(0.8, wallBand));
        c.Position += frame.Right * push;
        lateral = Vector3.Dot(c.Position - frame.Position, frame.Right);
        if (Math.Abs(lateral) > half)
        {
            var snap = (float)(lateral - Math.CopySign(half * 0.92, lateral));
            c.Position -= frame.Right * snap;
        }

        if (c.Health <= 0)
            Crash(c);
    }

    private static void Crash(HoverCraftState c)
    {
        c.Crashed = true;
        c.Speed = 0;
        c.Health = 0;
        c.Boosting = false;
    }

    private static void UpdateShield(HoverCraftState c)
    {
        if (!c.ShieldActive)
            return;
        c.ShieldTimer -= (float)DeltaTime;
        if (c.ShieldTimer <= 0)
        {
            c.ShieldActive = false;
            c.ShieldTimer = 0;
        }
    }

    private void CollectPickups(HoverCraftState c)
    {
        foreach (var pad in State.Pickups)
        {
            if (!pad.Available)
                continue;
            if (Vector3.Distance(Flat(c.Position), Flat(pad.Position)) > PickupRadius)
                continue;

            pad.Available = false;
            pad.RespawnTimer = 8f;
            switch (pad.Kind)
            {
                case PickupKind.Weapon:
                    c.WeaponAmmo = Math.Min(5, c.WeaponAmmo + 2);
                    break;
                case PickupKind.Shield:
                    c.ShieldActive = true;
                    c.ShieldTimer = 4.5f;
                    break;
            }

            PickupCollected?.Invoke(c, pad);
        }
    }

    private void FireWeapon(HoverCraftState c)
    {
        c.WeaponAmmo--;
        State.Projectiles.Add(new HoverProjectile
        {
            OwnerId = c.Id,
            Position = c.Position + c.Forward * 1.5f,
            Velocity = c.Forward * ProjectileSpeed,
        });
        WeaponFired?.Invoke(c);
    }

    private void UpdateProjectiles()
    {
        for (var i = State.Projectiles.Count - 1; i >= 0; i--)
        {
            var p = State.Projectiles[i];
            if (!p.Active)
            {
                State.Projectiles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * (float)DeltaTime;
            p.Life -= (float)DeltaTime;
            if (p.Life <= 0)
            {
                State.Projectiles.RemoveAt(i);
                continue;
            }

            foreach (var c in State.Craft)
            {
                if (c.Id == p.OwnerId || c.Crashed || c.Finished)
                    continue;
                if (Vector3.Distance(c.Position, p.Position) > ProjectileHitRadius)
                    continue;

                p.Active = false;
                var damage = c.ShieldActive ? 8.0 : 28.0;
                c.Health -= damage;
                if (c.ShieldActive)
                {
                    c.ShieldActive = false;
                    c.ShieldTimer = 0;
                }

                var owner = State.Craft.FirstOrDefault(x => x.Id == p.OwnerId);
                if (owner is not null)
                    WeaponHit?.Invoke(owner, c);

                if (c.Health <= 0)
                    Crash(c);

                State.Projectiles.RemoveAt(i);
                break;
            }
        }
    }

    private void UpdatePickupRespawns()
    {
        foreach (var pad in State.Pickups)
        {
            if (pad.Available)
                continue;
            pad.RespawnTimer -= (float)DeltaTime;
            if (pad.RespawnTimer <= 0)
            {
                pad.Available = true;
                pad.RespawnTimer = 0;
            }
        }
    }

    private void UpdatePlaces()
    {
        var ranked = State.Craft
            .Select((c, i) => (c, i))
            .OrderByDescending(x => x.c.TrackProgress)
            .ThenBy(x => x.c.Crashed)
            .ToList();
        for (var place = 0; place < ranked.Count; place++)
            ranked[place].c.Place = place + 1;
    }

    private RaceStanding GetStanding(HoverCraftState c) =>
        new(c.Place, c.Id, c.Name, c.CompletedLaps, c.TrackProgress);

    private void AccumulateReward(
        int index,
        HoverCraftState before,
        HoverCraftState after,
        TrackProgressSample progressBefore,
        TrackProgressSample progressAfter)
    {
        if (_rewardModel is null || _rewardAccum is null)
            return;

        var prevCar = ToCarState(before);
        var currCar = ToCarState(after);
        var breakdown = _rewardModel.Evaluate(Track, prevCar, currCar, progressBefore, progressAfter);
        _rewardAccum[index] += breakdown.Total;
    }

    private HoverRaceWorldState CreateInitialState(int targetLaps)
    {
        var craft = new List<HoverCraftState>(Controllers.Count);
        var startFrame = _frames.Length > 0 ? _frames[0] : default;
        for (var i = 0; i < Controllers.Count; i++)
        {
            var lateral = (i - (Controllers.Count - 1) * 0.5f) * 2.4f;
            var along = -i * 3.2f;
            var pos = startFrame.Position
                      + startFrame.Right * lateral
                      + startFrame.Tangent * along
                      + startFrame.Up * HoverHeight;
            if (_frames.Length == 0)
            {
                var start = Track.StartPose;
                var n = new Vector3(start.Forward.Z, 0f, -start.Forward.X);
                pos = start.Position + n * lateral - start.Forward * (i * 2.2f);
                pos = new Vector3(pos.X, HoverHeight, pos.Z);
            }

            craft.Add(new HoverCraftState
            {
                Id = i,
                Name = Controllers[i].Name,
                Position = pos,
                PreviousPosition = pos,
                Forward = _frames.Length > 0
                    ? startFrame.Tangent
                    : Vector3.Normalize(new Vector3(Track.StartPose.Forward.X, 0f, Track.StartPose.Forward.Z)),
                Bank = startFrame.TwistRadians / MathF.PI,
                WeaponAmmo = i == 0 ? 1 : 0,
            });
        }

        var pickups = BuildPickups();
        return new HoverRaceWorldState
        {
            Craft = craft,
            Pickups = pickups,
            TargetLaps = targetLaps,
        };
    }

    private List<TrackPickup> BuildPickups()
    {
        var list = new List<TrackPickup>();
        if (_frames.Length == 0)
            return list;

        var step = Math.Max(1, _frames.Length / 8);
        for (var i = step / 2; i < _frames.Length; i += step)
        {
            var f = _frames[i];
            list.Add(new TrackPickup
            {
                Id = list.Count,
                Kind = list.Count % 2 == 0 ? PickupKind.Weapon : PickupKind.Shield,
                Position = f.Position + f.Up * (HoverHeight + 0.4f),
            });
        }

        return list;
    }

    private static Vector3 Flat(Vector3 p) => new(p.X, 0f, p.Z);

    private static CarState ToCarState(HoverCraftState c) =>
        new()
        {
            Id = c.Id,
            Name = c.Name,
            Position = Flat(c.Position),
            PreviousPosition = Flat(c.PreviousPosition),
            Forward = new Vector3(c.Forward.X, 0f, c.Forward.Z),
            Speed = c.Speed,
            SteeringAngle = 0,
            Crashed = c.Crashed,
            WrongWay = false,
            CompletedLaps = c.CompletedLaps,
            CurrentGateIndex = c.CurrentGateIndex,
            TrackProgress = c.TrackProgress,
            Fitness = 0,
            TicksAlive = c.TicksAlive,
            WrongWayTicks = 0,
        };
}
