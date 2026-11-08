using System.Globalization;
using Novolis.Agent.Core;

namespace ArtillerySimulator;

internal static class AgentToBattleCommandMapper
{
    public static bool TryBuildBattleCommand(
        AgentCommand command,
        Func<AgentCommand, string, string, AgentCommandResult> reject,
        out BattleCommand battleCommand,
        out AgentCommandResult? rejectResult)
    {
        battleCommand = null!;
        rejectResult = null;
        var done = new TaskCompletionSource<BattleCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        switch (command.ActionId)
        {
            case "artillery.aim":
                if (!TryNumber(command, "elevation", out var elevation)
                    || !TryNumber(command, "azimuth", out var azimuth)
                    || !IsFinite(elevation)
                    || !IsFinite(azimuth))
                {
                    rejectResult = reject(command, "invalid-arguments", "Aim needs finite elevation and azimuth.");
                    return false;
                }

                battleCommand = BattleCommand.Aim((float)elevation, (float)azimuth, command.CorrelationId, done);
                return true;

            case "artillery.charge":
                if (!TryNumber(command, "index", out var index) || !IsFinite(index))
                {
                    rejectResult = reject(command, "invalid-arguments", "Charge needs a finite index.");
                    return false;
                }

                battleCommand = BattleCommand.Charge((int)Math.Round(index), command.CorrelationId, done);
                return true;

            case "artillery.camera":
                if (!command.Params.TryGetValue("mode", out var mode))
                {
                    rejectResult = reject(command, "invalid-arguments", "Camera needs mode shell|follow or gun.");
                    return false;
                }

                var followShell = mode.Equals("shell", StringComparison.OrdinalIgnoreCase)
                    || mode.Equals("follow", StringComparison.OrdinalIgnoreCase)
                    || mode.Equals("projectile", StringComparison.OrdinalIgnoreCase);
                battleCommand = BattleCommand.FollowCamera(followShell, command.CorrelationId, done);
                return true;

            case "artillery.fire":
                battleCommand = BattleCommand.Fire(command.CorrelationId, done);
                return true;

            default:
                rejectResult = reject(command, "unavailable", "Unknown action.");
                return false;
        }
    }

    private static bool TryNumber(AgentCommand command, string name, out double value)
    {
        value = 0;
        return command.Params.TryGetValue(name, out var text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}
