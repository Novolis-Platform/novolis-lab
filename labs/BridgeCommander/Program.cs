using System.Text.Json;
using BridgeCommander.Bridge;
using BridgeCommander.Bridge.Mcp;

if (args.Contains("--mcp", StringComparer.OrdinalIgnoreCase))
{
    await BridgeMcpHost.RunStdioAsync(args);
    return;
}

if (args.Contains("--qa-smoke", StringComparer.OrdinalIgnoreCase))
{
    Environment.Exit(await BridgeMcpHost.RunQaSmokeAsync());
    return;
}

if (args.Contains("--mcp-test", StringComparer.OrdinalIgnoreCase))
{
    Environment.Exit(await BridgeMcpIntegrationTest.RunAsync());
    return;
}

if (args.Contains("--mcp-play", StringComparer.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Use: dotnet run --file scripts/bridge-mcp-play.cs (from labs/BridgeCommander)");
    Environment.Exit(1);
    return;
}

var noVoice = args.Contains("--no-voice", StringComparer.OrdinalIgnoreCase);
var interactive = args.Contains("--interactive", StringComparer.OrdinalIgnoreCase);
var patrol = args.Contains("--patrol", StringComparer.OrdinalIgnoreCase);

var transmitIndex = Array.FindIndex(args, a => string.Equals(a, "--transmit", StringComparison.OrdinalIgnoreCase));
if (transmitIndex >= 0 && transmitIndex + 1 < args.Length)
{
    var sessionOptions = noVoice ? BridgeSessionOptions.WithoutVoice : BridgeSessionOptions.Default;
    await using var cliSession = BridgeSession.Create(sessionOptions);
    var result = await cliSession.TransmitAsync(string.Join(' ', args[(transmitIndex + 1)..]));
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

var sessionOptionsMain = noVoice
    ? BridgeSessionOptions.WithoutVoice
    : interactive
        ? BridgeSessionOptions.ForInteractive
        : BridgeSessionOptions.ForExchange;

await using var session = BridgeSession.Create(sessionOptionsMain);

if (interactive)
    await BridgeSpectreApp.RunInteractiveAsync(session);
else
    await BridgeExchangeRunner.RunAsync(session, starTrek: !patrol);
