using Novolis.Audio.Voice;

namespace BridgeCommander.Bridge;

/// <summary>Speaks bridge lines via ATC voice (blocking or fire-and-forget).</summary>
public sealed class BridgeVoiceAnnouncer(IVoiceService? voice, bool awaitPlayback)
{
    private readonly IVoiceService? _voice = voice;

    /// <summary>Speaks <paramref name="text"/> when voice is enabled.</summary>
    public ValueTask AnnounceAsync(string? text, CancellationToken cancellationToken = default)
    {
        if (_voice is null || string.IsNullOrWhiteSpace(text))
            return ValueTask.CompletedTask;

        var line = BridgeVoiceNarration.ForStatus(text);
        if (string.IsNullOrWhiteSpace(line))
            return ValueTask.CompletedTask;

        if (awaitPlayback)
            return SpeakCoreAsync(line, cancellationToken);

        _ = Task.Run(async () =>
        {
            try
            {
                await SpeakCoreAsync(line, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Spectre.Console.AnsiConsole.MarkupLine(
                    $"[red][[bridge-voice]][/] {Spectre.Console.Markup.Escape(ex.Message)}");
            }
        }, cancellationToken);

        return ValueTask.CompletedTask;
    }

    private async ValueTask SpeakCoreAsync(string line, CancellationToken cancellationToken)
    {
        if (_voice is null)
            return;

        try
        {
            await _voice.SpeakAsync(line, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Spectre.Console.AnsiConsole.MarkupLine(
                $"[red][[bridge-voice]][/] {Spectre.Console.Markup.Escape(ex.Message)}");
        }
    }
}
