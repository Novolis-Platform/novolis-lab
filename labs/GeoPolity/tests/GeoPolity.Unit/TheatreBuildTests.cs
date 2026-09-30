using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GeoPolity.Agent;
using GeoPolity.AvaloniaUi;
using GeoPolity.Session;
using Novolis.Agent.Surface;
using Novolis.Geopolitics.Core;
using Novolis.Geopolitics.Scenarios;

namespace GeoPolity.Unit;

public sealed class TheatreBuildTests
{
    [Test]
    public async Task TheatreMapProjection_Layout_IsNonDegenerate()
    {
        var world = DefaultWorld.Load();
        var (points, _) = TheatreMapProjection.Project(world);
        await Assert.That(points.Count).IsEqualTo(world.Polities.Count);
        await Assert.That(points.Max(p => p.X)).IsGreaterThan(0);
        await Assert.That(points.Max(p => p.Y)).IsGreaterThan(0);
        await Assert.That(points.Select(p => (p.X, p.Y)).Distinct().Count())
            .IsEqualTo(world.Polities.Count);
    }

    [Test]
    public async Task SelectSystem_UpdatesFocus()
    {
        var session = new GeoSession(ProceduralWorldGenerator.Generate(31), rngSeed: 31);
        var other = session.World.Polities.First(p => p.Id != session.PlayerSystemId).Id;
        GeoSessionCommands.SelectSystem(session, other);
        await Assert.That(session.SelectedSystemId).IsEqualTo(other);
        await Assert.That(session.Clock.StatusNote).Contains("focus");
    }

    [Test]
    public async Task OrderBuild_SpendsTreasuryAndRaisesForce_PlayerOnly()
    {
        var session = new GeoSession(ProceduralWorldGenerator.Generate(32), rngSeed: 32);
        var player = session.Player;
        var other = session.World.Polities.First(p => p.Id != session.PlayerSystemId);
        var otherNavalBefore = other.Military.Naval;

        player.Treasury = 50_000;
        var treasuryBefore = player.Treasury;
        var navalBefore = player.Military.Naval;
        var amount = 10d;
        var expectedCost = amount * MilitaryBuildCosts.UnitCost(MilitaryDomain.Naval);

        var msg = GeoSessionCommands.OrderBuild(session, MilitaryDomain.Naval, amount);

        await Assert.That(msg).Contains("Naval");
        await Assert.That(player.Treasury).IsEqualTo(treasuryBefore - expectedCost);
        await Assert.That(player.Military.Naval).IsEqualTo(navalBefore + amount);
        await Assert.That(other.Military.Naval).IsEqualTo(otherNavalBefore);
        await Assert.That(session.World.Events.Any(e => e.Kind == GeoEventKind.ForceExpansion)).IsTrue();
    }

    [Test]
    public async Task OrderBuild_InsufficientTreasury_NoForceChange()
    {
        var session = new GeoSession(ProceduralWorldGenerator.Generate(33), rngSeed: 33);
        var player = session.Player;
        player.Treasury = 1;
        var navalBefore = player.Military.Naval;

        var msg = GeoSessionCommands.OrderBuild(session, MilitaryDomain.Naval, 10);

        await Assert.That(msg).Contains("insufficient");
        await Assert.That(player.Military.Naval).IsEqualTo(navalBefore);
        await Assert.That(player.Treasury).IsEqualTo(1);
    }

    [Test]
    public async Task TheatreMapProjection_PointCount_MatchesPolities()
    {
        var world = ProceduralWorldGenerator.Generate(34);
        var (points, edges) = TheatreMapProjection.Project(world);
        await Assert.That(points.Count).IsEqualTo(world.Polities.Count);
        await Assert.That(edges.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task AgentSurface_DocumentAndBuildCommand()
    {
        var session = new GeoSession(ProceduralWorldGenerator.Generate(35), rngSeed: 35);
        session.Player.Treasury = 50_000;
        var navalBefore = session.Player.Military.Naval;
        var port = GetFreePort();
        var host = new GeoPolityAgentHost(session);

        await using var http = AgentHttpHost.Attach(host, GeoPolitySessionContract.Definition, port);
        using var client = new HttpClient { BaseAddress = new Uri(http.BaseUrl.TrimEnd('/') + "/") };

        using var docResp = await client.GetAsync("agent/document");
        var docJson = await docResp.Content.ReadAsStringAsync();
        await Assert.That(docResp.IsSuccessStatusCode).IsTrue()
            .Because($"document {docResp.StatusCode}: {docJson}");
        await Assert.That(docJson).Contains("geopolity");
        await Assert.That(docJson).Contains("build");
        await Assert.That(docJson).Contains("selectsystem");

        using var content = new StringContent(
            """{"actionId":"build","params":{"domain":"naval","amount":"10"}}""",
            Encoding.UTF8,
            "application/json");
        using var cmdResp = await client.PostAsync("agent/command", content);
        var cmdJson = await cmdResp.Content.ReadAsStringAsync();
        await Assert.That(cmdResp.IsSuccessStatusCode).IsTrue()
            .Because($"command {cmdResp.StatusCode}: {cmdJson}");
        using var resultDoc = JsonDocument.Parse(cmdJson);
        await Assert.That(resultDoc.RootElement.GetProperty("ok").GetBoolean()).IsTrue();
        await Assert.That(session.Player.Military.Naval).IsEqualTo(navalBefore + 10);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
