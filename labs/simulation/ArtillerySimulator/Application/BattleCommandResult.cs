using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

/// <summary>Outcome of applying one <see cref="BattleCommand"/> on the frame thread.</summary>
internal sealed class BattleCommandResult
{
    public static BattleCommandResult Ok(string message, BallisticTrajectoryRunner? firedRound = null) =>
        new() { Succeeded = true, Message = message, FiredRound = firedRound };

    public static BattleCommandResult Fail(string errorCode, string message) =>
        new() { Succeeded = false, ErrorCode = errorCode, Message = message };

    public bool Succeeded { get; private init; }
    public string Message { get; private init; } = "";
    public string? ErrorCode { get; private init; }
    public BallisticTrajectoryRunner? FiredRound { get; private init; }
}
