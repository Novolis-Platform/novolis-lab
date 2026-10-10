namespace BridgeCommander.Bridge.Mcp;

internal static class BridgeMcpRuntime
{
    private static readonly Lock Gate = new();
    private static BridgeSession? _session;

    public static BridgeSession Session
    {
        get
        {
            lock (Gate)
                return _session ??= BridgeSession.Create(BridgeSessionOptions.WithoutVoice);
        }
    }

    public static void ResetSession()
    {
        lock (Gate)
        {
            _session?.Initialize();
            _session ??= BridgeSession.Create(BridgeSessionOptions.WithoutVoice);
        }
    }

    public static async Task ReplaceSessionAsync()
    {
        BridgeSession? previous;
        lock (Gate)
        {
            previous = _session;
            _session = BridgeSession.Create(BridgeSessionOptions.WithoutVoice);
        }

        if (previous is not null)
            await previous.DisposeAsync().ConfigureAwait(false);
    }
}
