using Novolis.Commands;

namespace BridgeCommander.Bridge;

public sealed class BridgeCommandProcessor(BridgeActivityTracker activity, BridgeVoiceAnnouncer announcer)
    : ICommandProcessor<BridgeState>
{
    public async ValueTask ProcessAsync(
        CommandEnvelope command,
        BridgeState state,
        CancellationToken cancellationToken)
    {
        try
        {
            await ProcessCoreAsync(command, state, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            activity.OnFinished();
        }
    }

    private async ValueTask ProcessCoreAsync(
        CommandEnvelope command,
        BridgeState state,
        CancellationToken cancellationToken)
    {
        state.AddHistory(new HistoryEntry(
            DateTimeOffset.UtcNow,
            command.OriginalPrompt,
            HistoryKind.Executing,
            $"Running {command.Name}…"));

        try
        {
            if (command.Name == BuiltInCommands.BelayThat)
            {
                state.StatusLine = "Belay that! Current action cancelled.";
                state.AddHistory(new HistoryEntry(
                    DateTimeOffset.UtcNow,
                    command.OriginalPrompt,
                    HistoryKind.Interrupted,
                    "Emergency belay acknowledged."));
                await announcer.AnnounceAsync(state.StatusLine, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (command.Name == BuiltInCommands.ClearQueue)
            {
                state.StatusLine = "Command queue cleared.";
                state.AddHistory(new HistoryEntry(
                    DateTimeOffset.UtcNow,
                    command.OriginalPrompt,
                    HistoryKind.Executed,
                    "Queued orders dismissed."));
                return;
            }

            if (command.Name == BuiltInCommands.Help)
                return;

            if (command.Name == BuiltInCommands.RepeatLast)
            {
                if (state.LastEnvelope is null)
                {
                    state.StatusLine = "Nothing to repeat.";
                    return;
                }

                state.StatusLine = $"Repeating: {state.LastExecutedCommand}";
                await ExecuteDomainAsync(state.LastEnvelope, state, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (await ExecuteDomainAsync(command, state, cancellationToken).ConfigureAwait(false))
            {
                state.LastEnvelope = command;
                state.LastExecutedCommand = command.Name;
            }
        }
        catch (OperationCanceledException)
        {
            state.StatusLine = "Command interrupted.";
            state.AddHistory(new HistoryEntry(
                DateTimeOffset.UtcNow,
                command.OriginalPrompt,
                HistoryKind.Interrupted,
                $"{command.Name} cancelled."));
        }
    }

    private async ValueTask<bool> ExecuteDomainAsync(
        CommandEnvelope command,
        BridgeState state,
        CancellationToken cancellationToken)
    {
        switch (command.Name)
        {
            case "helm.set-heading":
                state.Heading = NormalizeHeading(Convert.ToDouble(command.Arguments["heading"]!));
                if (command.Arguments.TryGetValue("headingBy", out var byValue) && byValue is not null)
                    state.HeadingBy = NormalizeHeading(Convert.ToDouble(byValue));
                state.StatusLine = command.Arguments.ContainsKey("headingBy")
                    ? $"Helm: course {state.Heading:0.##}° BY {state.HeadingBy:0.##}°."
                    : $"Helm: course set to {state.Heading:0.##}°.";
                await SimulateWork(TimeSpan.FromSeconds(1.2), cancellationToken);
                break;

            case "helm.come-about":
                state.Heading = NormalizeHeading(state.Heading + 180);
                state.StatusLine = $"Helm: coming about to {state.Heading:0.##}°.";
                await SimulateWork(TimeSpan.FromSeconds(1.2), cancellationToken);
                break;

            case "helm.all-ahead-full":
                state.SpeedWarp = 9;
                state.StatusLine = "Helm: all ahead full — warp 9.";
                await SimulateWork(TimeSpan.FromSeconds(1), cancellationToken);
                break;

            case "helm.full-stop":
                state.SpeedWarp = 0;
                state.StatusLine = "Helm: all stop.";
                await SimulateWork(TimeSpan.FromMilliseconds(600), cancellationToken);
                break;

            case "helm.set-speed":
                state.SpeedWarp = Math.Clamp((int)command.Arguments["warp"]!, 0, 9);
                state.StatusLine = $"Helm: warp {state.SpeedWarp}.";
                await SimulateWork(TimeSpan.FromSeconds(1), cancellationToken);
                break;

            case "tactical.lock-target":
                state.TargetLocked = true;
                state.TargetName = "Hostile frigate KR-12";
                state.StatusLine = "Tactical: target locked.";
                await SimulateWork(TimeSpan.FromMilliseconds(800), cancellationToken);
                break;

            case "tactical.fire-weapons":
                if (!state.TargetLocked)
                {
                    state.StatusLine = "Tactical: no target lock.";
                    state.AddHistory(new HistoryEntry(
                        DateTimeOffset.UtcNow,
                        command.OriginalPrompt,
                        HistoryKind.Executed,
                        state.StatusLine));
                    return false;
                }

                state.StatusLine = $"Tactical: weapons fired at {state.TargetName}.";
                await SimulateWork(TimeSpan.FromSeconds(2.5), cancellationToken);
                break;

            case "engineering.divert-shields":
                state.ShieldPercent = Math.Min(100, state.ShieldPercent + 15);
                state.StatusLine = "Engineering: power diverted to shields.";
                await SimulateWork(TimeSpan.FromSeconds(2), cancellationToken);
                break;

            case "engineering.divert-weapons":
                state.ShieldPercent = Math.Max(0, state.ShieldPercent - 10);
                state.StatusLine = "Engineering: power diverted to weapons.";
                await SimulateWork(TimeSpan.FromSeconds(2), cancellationToken);
                break;

            case "engineering.repair":
                state.HullPercent = Math.Min(100, state.HullPercent + 8);
                state.StatusLine = "Engineering: hull repair underway.";
                await SimulateWork(TimeSpan.FromSeconds(3), cancellationToken);
                break;

            case "nav.set-course":
                state.StatusLine = $"Nav: course laid in for {command.Arguments["destination"]}.";
                await SimulateWork(TimeSpan.FromSeconds(1.5), cancellationToken);
                break;

            case "comms.hail":
                state.StatusLine = "Comms: hail transmitted on open channel.";
                await SimulateWork(TimeSpan.FromMilliseconds(900), cancellationToken);
                break;

            case "crew.dismiss-personnel":
                state.StatusLine = "Admin: personnel transfer logged.";
                await SimulateWork(TimeSpan.FromMilliseconds(500), cancellationToken);
                break;

            default:
                state.StatusLine = $"Unknown command handler: {command.Name}";
                break;
        }

        state.AddHistory(new HistoryEntry(
            DateTimeOffset.UtcNow,
            command.OriginalPrompt,
            HistoryKind.Executed,
            state.StatusLine));

        await announcer.AnnounceAsync(state.StatusLine, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static async Task SimulateWork(TimeSpan duration, CancellationToken cancellationToken) =>
        await Task.Delay(duration, cancellationToken);

    private static double NormalizeHeading(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
