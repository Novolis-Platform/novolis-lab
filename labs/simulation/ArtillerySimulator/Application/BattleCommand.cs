namespace ArtillerySimulator;

/// <summary>
/// Discrete battery intent. Reference type for <c>Novolis.Messaging.Channels.AddChannel</c>.
/// Continuous lay (WASD) stays sync on <see cref="IBatteryApplier"/> — no per-frame alloc.
/// </summary>
internal sealed class BattleCommand
{
    public required BattleCommandKind Kind { get; init; }
    public float ElevationDegrees { get; init; }
    public float AzimuthDegrees { get; init; }
    public int ChargeIndex { get; init; }
    public bool FollowShell { get; init; }
    public string? CorrelationId { get; init; }
    public TaskCompletionSource<BattleCommandResult>? Completion { get; init; }

    public static BattleCommand Aim(float elevationDegrees, float azimuthDegrees, string? correlationId = null, TaskCompletionSource<BattleCommandResult>? completion = null) =>
        new()
        {
            Kind = BattleCommandKind.Aim,
            ElevationDegrees = elevationDegrees,
            AzimuthDegrees = azimuthDegrees,
            CorrelationId = correlationId,
            Completion = completion,
        };

    public static BattleCommand Charge(int index, string? correlationId = null, TaskCompletionSource<BattleCommandResult>? completion = null) =>
        new()
        {
            Kind = BattleCommandKind.Charge,
            ChargeIndex = index,
            CorrelationId = correlationId,
            Completion = completion,
        };

    public static BattleCommand Fire(string? correlationId = null, TaskCompletionSource<BattleCommandResult>? completion = null) =>
        new()
        {
            Kind = BattleCommandKind.Fire,
            CorrelationId = correlationId,
            Completion = completion,
        };

    public static BattleCommand FollowCamera(bool followShell, string? correlationId = null, TaskCompletionSource<BattleCommandResult>? completion = null) =>
        new()
        {
            Kind = BattleCommandKind.FollowCamera,
            FollowShell = followShell,
            CorrelationId = correlationId,
            Completion = completion,
        };

    public static BattleCommand CycleGun(string? correlationId = null, TaskCompletionSource<BattleCommandResult>? completion = null) =>
        new()
        {
            Kind = BattleCommandKind.CycleGun,
            CorrelationId = correlationId,
            Completion = completion,
        };
}
