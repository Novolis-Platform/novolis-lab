#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:package Novolis.Agent.Core@2026.1.*
#:package Novolis.Agent.Surface@2026.1.*

// Drive ArtillerySimulator over the agent HTTP surface (start the game with --headless first).
//   dotnet run --project d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator -p:NovolisUseProjectReferences=true -c Release -- --headless
//   dotnet run --file d:\novolis\novolis-lab\labs\simulation\ArtillerySimulator\scripts\artillery-agent-play.cs -- --rounds 5

using System.Globalization;
using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;

var baseUrl = args.FirstOrDefault(static a => a.StartsWith("http", StringComparison.OrdinalIgnoreCase))
    ?? "http://127.0.0.1:18770";
var rounds = 12;
var stopAfterMissionRound = 3;
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] is "--rounds" && int.TryParse(args[i + 1], out var n) && n > 0)
        rounds = n;
    if (args[i] is "--until-round" && int.TryParse(args[i + 1], out var r) && r > 0)
        stopAfterMissionRound = r;
}

await using var client = new AgentHttpClient(baseUrl);
Console.WriteLine($"Connecting {baseUrl} …");

await WaitHealthyAsync(baseUrl);
var hello = await client.HelloAsync();
Console.WriteLine($"HELLO  {hello.AppTitle}  pid={hello.ProcessId}  surface={hello.SurfaceId}");
var caps = hello.Capabilities ?? Array.Empty<string>();
Console.WriteLine($"  caps: {string.Join(", ", caps)}");

var actions = await client.ActionsAsync();
var actionList = actions.Actions ?? Array.Empty<AgentAction>();
Console.WriteLine($"ACTIONS ({actionList.Length})");
foreach (var a in actionList)
    Console.WriteLine($"  {(a.Enabled ? "OK" : "NO")} {a.Id}  {a.Summary}{(a.DisabledReason is { } d ? $"  [{d}]" : "")}");

var mcp = await client.McpToolsAsync();
var mcpText = mcp.GetRawText();
Console.WriteLine($"MCP tools payload: {(mcpText.Length <= 240 ? mcpText : mcpText[..240] + "…")}");

var kills = 0;
var pins = 0;
var shots = 0;
var maxMissionRoundSeen = 1;

for (var volley = 1; volley <= rounds; volley++)
{
    Console.WriteLine();
    Console.WriteLine($"=== VOLLEY {volley}/{rounds} ===");
    var snap = await client.SnapshotAsync();
    PrintSnapshot(snap);
    maxMissionRoundSeen = Math.Max(maxMissionRoundSeen, ParseMissionRound(snap));
    if (maxMissionRoundSeen > stopAfterMissionRound)
    {
        Console.WriteLine($"Reached mission round {maxMissionRoundSeen} — stopping after requested waves.");
        break;
    }

    var threat = PickThreat(snap);
    if (threat is null)
    {
        Console.WriteLine("No live threats — waiting for next wave…");
        await Task.Delay(2000);
        continue;
    }

    var gunX = ParseFloat(snap, "gunX") ?? (float)snap.MapX;
    var gunZ = ParseFloat(snap, "gunZ") ?? (float)snap.MapY;
    var range = Horizontal(gunX, gunZ, (float)threat.X, (float)threat.Z);
    var bearing = BearingDegrees(gunX, gunZ, (float)threat.X, (float)threat.Z);
    // Columns close ~12.5 m/s; steep low-charge TOF ~50–60 s → lead short by ~650 m.
    var leadMeters = Math.Clamp(range * 0.08f, 450f, 750f);
    var aimRange = Math.Max(4_200f, range - leadMeters);

    // Prefer low/reduced first (OpeningFirePlan order). Walk elevation against predictedMeters.
    var charge = aimRange < 7_500f ? 3 : 0;
    var elev = aimRange < 7_500f ? 55f : 45f;

    Console.WriteLine(string.Create(
        CultureInfo.InvariantCulture,
        $"TARGET  {threat.Name}#{threat.Id}  {threat.State}  now={range / 1000f:F2} km  aim={aimRange / 1000f:F2} km (lead {leadMeters:F0} m)  bearing={bearing:F0} deg  missionRound={ParseMissionRound(snap)}"));

    var rev = snap.StateRevision;
    (elev, charge, rev, snap) = await BracketOntoTargetAsync(client, elev, charge, bearing, aimRange, rev);
    Console.WriteLine(string.Create(
        CultureInfo.InvariantCulture,
        $"LAY     elev={elev:F1} deg  az={bearing:F1} deg  charge={charge}  predicted={Status(snap, "predictedMeters")} m"));

    await WaitCrewReadyAsync(client, timeoutSeconds: 30);
    snap = await client.SnapshotAsync();
    rev = snap.StateRevision;
    PrintSnapshot(snap);

    var fireAction = (snap.Actions ?? []).FirstOrDefault(static a => a.Id == "artillery.fire");
    if (fireAction is { Enabled: false })
    {
        Console.WriteLine($"FIRE blocked: {fireAction.DisabledReason}");
        continue;
    }

    Console.WriteLine("FIRE    round away (impact is async)…");
    var fire = await client.CommandAsync(new AgentCommand
    {
        ActionId = "artillery.fire",
        CorrelationId = $"fire-{volley}-{Guid.NewGuid():N}",
        ExpectedRevision = rev,
    });
    shots++;
    Console.WriteLine($"FIRE    ok={fire.Ok}  {fire.Message}");
    if (!fire.Ok)
        continue;

    // Fire no longer blocks on impact — poll until the active shot leaves InFlight.
    var after = await WaitImpactAsync(client, timeoutSeconds: 90);
    ParseBattleResult(after.LastAction?.Message ?? after.Observations?.FirstOrDefault()?.Summary ?? "", ref kills, ref pins);
    PrintSnapshot(after);
    maxMissionRoundSeen = Math.Max(maxMissionRoundSeen, ParseMissionRound(after));
}

Console.WriteLine();
Console.WriteLine("=== FINAL ===");
var finalSnap = await client.SnapshotAsync();
PrintSnapshot(finalSnap);
maxMissionRoundSeen = Math.Max(maxMissionRoundSeen, ParseMissionRound(finalSnap));
Console.WriteLine($"SCORE  shots={shots}  kills≈{kills}  pins≈{pins}  maxMissionRound={maxMissionRoundSeen}");

static async Task<(float Elev, int Charge, long Rev, AgentSnapshot Snap)> BracketOntoTargetAsync(
    AgentHttpClient httpClient,
    float elev,
    int charge,
    float bearing,
    float targetMeters,
    long rev)
{
    // Seat charge once, then nudge elevation until predicted ≈ target (slightly short preferred).
    rev = await OrderChargeAsync(httpClient, charge, rev);
    for (var step = 0; step < 8; step++)
    {
        rev = await OrderAimAsync(httpClient, elev, bearing, rev);
        await Task.Delay(200);
        var snap = await httpClient.SnapshotAsync();
        rev = snap.StateRevision;
        var predicted = ParseFloat(snap, "predictedMeters");
        if (predicted is null)
            return (elev, charge, rev, snap);

        var error = predicted.Value - targetMeters;
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  bracket[{step}] elev={elev:F1} charge={charge} predicted={predicted.Value:F0} target={targetMeters:F0} err={error:F0}"));

        if (MathF.Abs(error) <= 120f || (error is > -180f and < 80f))
            return (elev, charge, rev, snap);

        // Too long → raise elevation (steeper, shorter) or drop to low charge.
        // Too short → lower elevation (flatter, longer) or bump charge.
        if (error > 200f)
        {
            if (elev < 68f)
                elev = MathF.Min(70f, elev + Math.Clamp(error / 400f, 1.5f, 6f));
            else if (charge != 3)
            {
                charge = 3;
                rev = await OrderChargeAsync(httpClient, charge, rev);
                elev = 55f;
            }
        }
        else if (error < -200f)
        {
            if (elev > 18f)
                elev = MathF.Max(14f, elev - Math.Clamp((-error) / 400f, 1.5f, 6f));
            else if (charge is 3)
            {
                charge = 0;
                rev = await OrderChargeAsync(httpClient, charge, rev);
                elev = 42f;
            }
            else if (charge is 0)
            {
                charge = 1;
                rev = await OrderChargeAsync(httpClient, charge, rev);
                elev = 38f;
            }
        }
        else
        {
            elev += error > 0 ? 1.5f : -1.5f;
            elev = Math.Clamp(elev, 14f, 70f);
        }
    }

    var final = await httpClient.SnapshotAsync();
    return (elev, charge, final.StateRevision, final);
}

static async Task<long> OrderAimAsync(AgentHttpClient httpClient, float elev, float az, long rev)
{
    var result = await httpClient.CommandAsync(new AgentCommand
    {
        ActionId = "artillery.aim",
        CorrelationId = $"aim-{Guid.NewGuid():N}",
        ExpectedRevision = rev,
        Params = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["elevation"] = elev.ToString("R", CultureInfo.InvariantCulture),
            ["azimuth"] = az.ToString("R", CultureInfo.InvariantCulture),
        },
    });
    Console.WriteLine($"AIM     ok={result.Ok}  {result.Message}");
    return result.StateRevision ?? rev;
}

static async Task<long> OrderChargeAsync(AgentHttpClient httpClient, int charge, long rev)
{
    var result = await httpClient.CommandAsync(new AgentCommand
    {
        ActionId = "artillery.charge",
        CorrelationId = $"chg-{Guid.NewGuid():N}",
        ExpectedRevision = rev,
        Params = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["index"] = charge.ToString(CultureInfo.InvariantCulture),
        },
    });
    Console.WriteLine($"CHARGE  ok={result.Ok}  {result.Message}");
    return result.StateRevision ?? rev;
}

static void ParseBattleResult(string message, ref int kills, ref int pins)
{
    // "The burst put N out of action and pinned M."
    var put = message.IndexOf("put ", StringComparison.Ordinal);
    var outOf = message.IndexOf(" out of action", StringComparison.Ordinal);
    if (put >= 0 && outOf > put && int.TryParse(message.AsSpan(put + 4, outOf - put - 4), out var k))
        kills += k;
    var pinIdx = message.IndexOf("pinned ", StringComparison.Ordinal);
    if (pinIdx >= 0)
    {
        var end = pinIdx + 7;
        while (end < message.Length && char.IsDigit(message[end]))
            end++;
        if (int.TryParse(message.AsSpan(pinIdx + 7, end - pinIdx - 7), out var p))
            pins += p;
    }
}

static async Task WaitHealthyAsync(string url)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    var deadline = DateTime.UtcNow.AddSeconds(90);
    Exception? last = null;
    while (DateTime.UtcNow < deadline)
    {
        try
        {
            using var resp = await http.GetAsync($"{url.TrimEnd('/')}/health");
            if (resp.IsSuccessStatusCode)
                return;
        }
        catch (Exception ex)
        {
            last = ex;
        }

        await Task.Delay(500);
    }

    throw new TimeoutException($"Agent HTTP not healthy at {url}. Last error: {last?.Message}");
}

static async Task WaitCrewReadyAsync(AgentHttpClient httpClient, int timeoutSeconds)
{
    var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
    while (DateTime.UtcNow < deadline)
    {
        var snap = await httpClient.SnapshotAsync();
        var reload = Status(snap, "reload");
        var crew = Status(snap, "crew");
        if (string.Equals(reload, "ready", StringComparison.OrdinalIgnoreCase)
            || string.Equals(crew, "ready", StringComparison.OrdinalIgnoreCase))
            return;
        Console.WriteLine($"  … crew={crew} reload={reload}");
        await Task.Delay(800);
    }
}

static async Task<AgentSnapshot> WaitImpactAsync(AgentHttpClient httpClient, int timeoutSeconds)
{
    var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
    AgentSnapshot? last = null;
    while (DateTime.UtcNow < deadline)
    {
        last = await httpClient.SnapshotAsync();
        var phase = Status(last, "phase");
        if (!string.Equals(phase, "InFlight", StringComparison.OrdinalIgnoreCase))
            return last;
        Console.WriteLine($"  … flight phase={phase} tof≈{Status(last, "flightSeconds")}");
        await Task.Delay(400);
    }

    return last ?? await httpClient.SnapshotAsync();
}

static void PrintSnapshot(AgentSnapshot snap)
{
    Console.WriteLine($"SNAP  rev={snap.StateRevision}  {snap.PauseReason}");
    if (snap.Observations is { Length: > 0 } obs)
        Console.WriteLine($"  {obs[0].Summary}");
    foreach (var key in new[]
             {
                 "enemies", "nearestMeters", "defenders", "defense", "crew", "reload", "predictedMeters",
                 "charge", "orderedElevation", "orderedAzimuth", "phase", "gunX", "gunZ",
                 "windMps", "windFrom", "tempC", "rh", "qnh", "metAge",
             })
    {
        var v = Status(snap, key);
        if (!string.IsNullOrEmpty(v))
            Console.WriteLine($"  {key}={v}");
    }

    var threats = (snap.Entities ?? [])
        .Where(static e => e.Kind == AgentEntity.Threat && e.State != "stopped")
        .ToList();
    Console.WriteLine($"  live threats: {threats.Count}");
    foreach (var t in threats.Take(5))
        Console.WriteLine($"    - {t.Name}  {t.State}  ({t.Summary})");
}

static AgentEntity? PickThreat(AgentSnapshot snap)
{
    var threats = (snap.Entities ?? [])
        .Where(static e => e.Kind == AgentEntity.Threat && e.State is not "stopped" and not "casualty")
        .ToList();
    if (threats.Count == 0)
        return null;

    return threats
        .OrderBy(static t => t.Summary.Contains("outside traverse", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
        .ThenBy(static t => ParseRangeKm(t.Summary))
        .First();
}

static int ParseMissionRound(AgentSnapshot snap)
{
    var text = snap.Observations is { Length: > 0 } obs ? obs[0].Summary : "";
    var idx = text.IndexOf("Round ", StringComparison.Ordinal);
    if (idx < 0)
        return 1;
    var start = idx + 6;
    var end = start;
    while (end < text.Length && char.IsDigit(text[end]))
        end++;
    return int.TryParse(text.AsSpan(start, end - start), out var round) ? round : 1;
}

static float ParseRangeKm(string summary)
{
    var idx = summary.IndexOf(" km from the gun", StringComparison.Ordinal);
    if (idx < 0)
        return 99f;
    var start = summary.LastIndexOf(' ', idx - 1);
    if (start < 0)
        return 99f;
    return float.TryParse(summary.AsSpan(start + 1, idx - start - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var km)
        ? km
        : 99f;
}

static float? ParseFloat(AgentSnapshot snap, string key)
{
    var s = Status(snap, key);
    return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
}

static string Status(AgentSnapshot snap, string key) =>
    snap.StatusLines is not null && snap.StatusLines.TryGetValue(key, out var v) ? v : "";

static float Horizontal(float ax, float az, float bx, float bz)
{
    var dx = bx - ax;
    var dz = bz - az;
    return MathF.Sqrt(dx * dx + dz * dz);
}

static float BearingDegrees(float ax, float az, float bx, float bz) =>
    MathF.Atan2(bz - az, bx - ax) * (180f / MathF.PI);
