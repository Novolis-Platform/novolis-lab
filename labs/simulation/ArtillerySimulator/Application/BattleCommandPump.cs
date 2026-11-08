using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace ArtillerySimulator;

/// <summary>
/// Drains <see cref="BattleCommand"/> on the Silk frame thread (single reader).
/// Completes agent <see cref="TaskCompletionSource{TResult}"/>s — no BackgroundService.
/// </summary>
internal sealed class BattleCommandPump
{
    private readonly ChannelReader<BattleCommand> _reader;
    private readonly IBatteryApplier _applier;
    private readonly ILogger<BattleCommandPump> _log;

    public BattleCommandPump(
        ChannelReader<BattleCommand> reader,
        IBatteryApplier applier,
        ILogger<BattleCommandPump> log)
    {
        _reader = reader;
        _applier = applier;
        _log = log;
    }

    public int Drain()
    {
        var n = 0;
        while (_reader.TryRead(out var command))
        {
            n++;
            BattleCommandResult result;
            try
            {
                result = _applier.Apply(command);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Battle command {Kind} failed", command.Kind);
                result = BattleCommandResult.Fail("exception", ex.Message);
            }

            command.Completion?.TrySetResult(result);
            if (_log.IsEnabled(LogLevel.Debug))
            {
                _log.LogDebug(
                    "Applied {Kind} ok={Ok} corr={CorrelationId}",
                    command.Kind,
                    result.Succeeded,
                    command.CorrelationId);
            }
        }

        return n;
    }
}
