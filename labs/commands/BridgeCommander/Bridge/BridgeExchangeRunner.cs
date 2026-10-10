namespace BridgeCommander.Bridge;

/// <summary>Runs the scripted bridge exchange with Spectre output and per-character voice.</summary>
public static class BridgeExchangeRunner
{
    public static async Task RunAsync(
        BridgeSession session,
        IReadOnlyList<BridgeExchangeBeat>? script = null,
        bool starTrek = true,
        CancellationToken cancellationToken = default)
    {
        script ??= starTrek
            ? StarTrekBridgeScript.RedAlertPatrol
            : BridgeExchangeScript.PatrolEngagement;

        var cast = session.VoiceCast;
        if (session.VoiceEnabled && cast is null)
            throw new InvalidOperationException("VoiceCast required for exchange (use BridgeSessionOptions.ForExchange).");

        BridgeSpectreUi.ShowTitle(starTrek);
        if (starTrek)
            BridgeSpectreUi.ShowCrewLegend();
        BridgeSpectreUi.RenderBridge(session.State);

        foreach (var beat in script)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BridgeSpectreUi.ShowBeat(beat);

            if (beat.VoiceLine is not null && cast is not null)
                await cast.SpeakAsync(beat.Character, beat.VoiceLine, cancellationToken).ConfigureAwait(false);

            if (beat.Transmit is not null)
            {
                await session.TransmitAsync(beat.Transmit, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                BridgeSpectreUi.RenderBridge(session.State);
            }

            if (beat.PauseAfterMs > 0)
                await Task.Delay(beat.PauseAfterMs, cancellationToken).ConfigureAwait(false);
        }

        BridgeSpectreUi.ShowClosing(starTrek);
    }
}
