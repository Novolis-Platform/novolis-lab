using System.Globalization;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace ArtillerySimulator.Game;

/// <summary>Scripted discover, aim, fire, replay, and watch against the artillery host.</summary>
internal static class ArtilleryAgentSession
{
    public static int Run()
    {
        Console.WriteLine("Building the 26 km range (seed 42)...");
        var host = new ArtilleryAgentHost();
        using var broker = new AgentEventBroker(host);
        var watch = broker.OpenPoll(new AgentWatchRequest
        {
            EventNames = [AgentMethodNames.Changed, AgentMethodNames.ActionResult],
            ResourceId = "artillery.gun",
        });
        if (!watch.Ok)
        {
            Console.WriteLine(watch.Error);
            return 1;
        }

        var hello = host.Hello();
        var opened = host.Snapshot();
        Console.WriteLine($"{hello.AppTitle}  surface={hello.SurfaceId}  contract={opened.MetadataVersion}  revision={opened.StateRevision}");
        Console.WriteLine($"watch {watch.WatchId} on artillery.gun");
        Console.WriteLine(opened.Observations[0].Summary);
        Console.WriteLine(opened.Line("atmosphere"));

        var revision = opened.StateRevision;
        if (!Expect(
                "reject aim without elevation",
                host.Execute(Command("artillery.aim", "aim-missing", revision, ("azimuth", "0"))),
                ok: false,
                reason: "invalid-arguments",
                revision))
        {
            return 1;
        }

        if (!Step(host, "aim 45°", Command("artillery.aim", "aim-45", revision, ("elevation", "45"), ("azimuth", "0")), ref revision))
        {
            return 1;
        }

        if (!Step(host, "reduced charge", Command("artillery.charge", "charge-reduced", revision, ("index", "0")), ref revision))
        {
            return 1;
        }

        var first = host.Execute(Command("artillery.fire", "fire-45", revision));
        if (!Report("fire 45°", first, expectOk: true))
        {
            return 1;
        }

        revision = first.StateRevision ?? revision;
        var firstRange = RangeOf(first);
        var replay = host.Execute(Command("artillery.fire", "fire-45", revision));
        if (!replay.Ok || replay.StateRevision != first.StateRevision || replay.Message != first.Message)
        {
            Console.WriteLine($"replay failed: ok={replay.Ok} rev={replay.StateRevision} {replay.Message}");
            return 1;
        }

        Console.WriteLine($"replay fire-45 stayed at revision {replay.StateRevision}");

        if (!Step(host, "aim 30°", Command("artillery.aim", "aim-30", revision, ("elevation", "30"), ("azimuth", "0")), ref revision))
        {
            return 1;
        }

        var second = host.Execute(Command("artillery.fire", "fire-30", revision));
        if (!Report("fire 30°", second, expectOk: true))
        {
            return 1;
        }

        revision = second.StateRevision ?? revision;
        var secondRange = RangeOf(second);
        if (firstRange is null || secondRange is null || Math.Abs(firstRange.Value - secondRange.Value) < 100)
        {
            Console.WriteLine("The two aims did not produce distinct impacts.");
            return 1;
        }

        var stale = host.Execute(Command("artillery.fire", "fire-stale", expectedRevision: 1));
        if (stale.Ok || stale.ErrorCode != "stale-snapshot" || stale.StateRevision != revision)
        {
            Console.WriteLine($"stale shot was not rejected: ok={stale.Ok} {stale.ErrorCode} rev={stale.StateRevision}");
            return 1;
        }

        Console.WriteLine($"stale fire rejected ({stale.ErrorCode}) and revision stayed {stale.StateRevision}");

        var delivered = broker.PollAsync(watch.WatchId, maxEvents: 64, TimeSpan.FromMilliseconds(500))
            .GetAwaiter()
            .GetResult();
        if (!delivered.Ok || delivered.Events.Length == 0)
        {
            Console.WriteLine(delivered.Error ?? "The watch delivered no events.");
            return 1;
        }

        Console.WriteLine($"watch.next delivered {delivered.Events.Length} events");
        foreach (var evt in delivered.Events)
        {
            Console.WriteLine($"  #{evt.EventSequence} {evt.EventName} {evt.CorrelationId} {evt.ResourceId}");
        }

        Console.WriteLine($"45° landed at {firstRange.Value:F0} m; 30° landed at {secondRange.Value:F0} m.");
        return 0;
    }

    private static bool Step(ArtilleryAgentHost host, string label, AgentCommand command, ref long revision)
    {
        var result = host.Execute(command);
        if (!Report(label, result, expectOk: true))
        {
            return false;
        }

        revision = result.StateRevision ?? revision;
        return true;
    }

    private static bool Expect(string label, AgentCommandResult result, bool ok, string reason, long revision)
    {
        var matched = result.Ok == ok && result.ErrorCode == reason && result.StateRevision == revision;
        Console.WriteLine($"{(matched ? "ok" : "FAIL")}  {label}: {result.ErrorCode}  revision {result.StateRevision}");
        return matched;
    }

    private static bool Report(string label, AgentCommandResult result, bool expectOk)
    {
        var mark = result.Ok == expectOk ? "ok" : "FAIL";
        Console.WriteLine($"{mark}  {label}: {result.Message}  revision {result.StateRevision}");
        return result.Ok == expectOk;
    }

    private static AgentCommand Command(string actionId, string correlationId, long? expectedRevision, params (string Name, string Value)[] args)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in args)
        {
            parameters[name] = value;
        }

        return new AgentCommand
        {
            ActionId = actionId,
            CorrelationId = correlationId,
            ExpectedRevision = expectedRevision,
            Params = parameters,
        };
    }

    private static double? RangeOf(AgentCommandResult result)
    {
        var text = result.Snapshot?.Line("rangeMeters");
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var meters) ? meters : null;
    }
}
