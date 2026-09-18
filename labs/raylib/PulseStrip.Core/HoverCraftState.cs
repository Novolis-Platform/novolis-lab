namespace PulseStrip.Core;

using System.Numerics;

/// <summary>Anti-grav craft pose and race scoring for PulseStrip.</summary>
public sealed class HoverCraftState
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required Vector3 Position { get; set; }
    public required Vector3 PreviousPosition { get; set; }
    public required Vector3 Forward { get; set; }
    public float Bank { get; set; }
    public double Speed { get; set; }
    public double BoostFuel { get; set; } = 1.0;
    public bool Boosting { get; set; }
    public bool Crashed { get; set; }
    public bool Finished { get; set; }
    public int CompletedLaps { get; set; }
    public int CurrentGateIndex { get; set; }
    public double TrackProgress { get; set; }
    public double Health { get; set; } = 100.0;
    public int WeaponAmmo { get; set; }
    public bool ShieldActive { get; set; }
    public float ShieldTimer { get; set; }
    public int TicksAlive { get; set; }
    public int Place { get; set; } = 1;

    public HoverCraftState CloneForComparison() =>
        new()
        {
            Id = Id,
            Name = Name,
            Position = Position,
            PreviousPosition = PreviousPosition,
            Forward = Forward,
            Bank = Bank,
            Speed = Speed,
            BoostFuel = BoostFuel,
            Boosting = Boosting,
            Crashed = Crashed,
            Finished = Finished,
            CompletedLaps = CompletedLaps,
            CurrentGateIndex = CurrentGateIndex,
            TrackProgress = TrackProgress,
            Health = Health,
            WeaponAmmo = WeaponAmmo,
            ShieldActive = ShieldActive,
            ShieldTimer = ShieldTimer,
            TicksAlive = TicksAlive,
            Place = Place,
        };
}
