using System.Text.Json;
using Novolis.Agent.Core;
using Novolis.Avalonia.ThreeD.Session;
using Novolis.ThreeD;

namespace SceneLab;

/// <summary>Headless spatial QA for describescene / groundphrase / setsceneprops / importtriangles.</summary>
internal static class SpatialSmoke
{
    public static int Run()
    {
        var scenePath = ResolveSample("spatial-smoke.nov3djson");
        var soupPath = ResolveSample("triangle-soup.json");
        if (scenePath is null)
        {
            Console.Error.WriteLine("spatial-smoke: spatial-smoke.nov3djson not found.");
            return 1;
        }

        var session = new SceneSessionService();
        var open = session.Execute(new AgentCommand
        {
            ActionId = SceneSessionActionIds.Open,
            Path = scenePath,
        });
        FailUnless(open, "open");

        var describe = session.Execute(new AgentCommand { ActionId = SceneSessionActionIds.DescribeScene });
        FailUnless(describe, "describescene");
        if (!describe.Message.Contains("Beacon", StringComparison.Ordinal) ||
            !describe.Message.Contains("WaypointAlpha", StringComparison.Ordinal) ||
            !describe.Message.Contains("Spatial Smoke", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("spatial-smoke: describescene missing expected names.");
            Console.Error.WriteLine(describe.Message);
            return 1;
        }

        var ground = session.Execute(new AgentCommand
        {
            ActionId = SceneSessionActionIds.GroundPhrase,
            Phrase = "Beacon",
            Select = true,
        });
        FailUnless(ground, "groundphrase");
        const string beaconId = "bbbbbbbb-0002-4000-8000-000000000002";
        if (!string.Equals(ground.NodeId, beaconId, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"spatial-smoke: expected Beacon id {beaconId}, got {ground.NodeId}");
            return 1;
        }

        if (session.Document.SelectionId?.ToString() is not { } sel ||
            !string.Equals(sel, beaconId, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("spatial-smoke: groundphrase did not select Beacon.");
            return 1;
        }

        var set = session.Execute(new AgentCommand
        {
            ActionId = SceneSessionActionIds.SetSceneProps,
            Key = "description",
            Value = "spatial-smoke ok",
        });
        FailUnless(set, "setsceneprops");

        var tmp = Path.Combine(Path.GetTempPath(), $"spatial-smoke-{Guid.NewGuid():N}.nov3djson");
        try
        {
            var save = session.Execute(new AgentCommand
            {
                ActionId = SceneSessionActionIds.Save,
                Path = tmp,
            });
            FailUnless(save, "save");

            var again = new SceneSessionService();
            var reopen = again.Execute(new AgentCommand
            {
                ActionId = SceneSessionActionIds.Open,
                Path = tmp,
            });
            FailUnless(reopen, "reopen");
            if (again.Document.Properties is null ||
                !again.Document.Properties.TryGetValue("description", out var desc) ||
                desc != "spatial-smoke ok")
            {
                Console.Error.WriteLine("spatial-smoke: properties did not round-trip.");
                return 1;
            }
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* ignore */ }
        }

        if (soupPath is not null)
        {
            var tri = session.Execute(new AgentCommand
            {
                ActionId = SceneSessionActionIds.ImportTriangles,
                Path = soupPath,
            });
            FailUnless(tri, "importtriangles");
            if (session.Document.Find(Guid.Parse(tri.NodeId!)) is not MeshNode mesh ||
                mesh.Vertices is null || mesh.Indices is null ||
                mesh.Vertices.Length < 9 || mesh.Indices.Length < 3)
            {
                Console.Error.WriteLine("spatial-smoke: importtriangles did not bake mesh.");
                return 1;
            }
        }

        // Touch JSON shape of groundphrase payload.
        using (var doc = JsonDocument.Parse(ground.Message))
        {
            if (!doc.RootElement.TryGetProperty("hitCount", out var hc) || hc.GetInt32() < 1)
            {
                Console.Error.WriteLine("spatial-smoke: groundphrase hitCount expected >= 1.");
                return 1;
            }
        }

        Console.WriteLine("SpatialSmoke OK");
        return 0;
    }

    private static void FailUnless(AgentCommandResult result, string label)
    {
        if (result.Ok)
            return;
        Console.Error.WriteLine($"spatial-smoke: {label} failed: {result.Message} ({result.ErrorCode})");
        Environment.Exit(1);
    }

    private static string? ResolveSample(string fileName)
    {
        foreach (var candidate in Candidates(fileName))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> Candidates(string fileName)
    {
        yield return Path.Combine(AppContext.BaseDirectory, "samples", fileName);
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "samples", fileName));
        yield return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "apps", "avalonia", "SceneLab", "samples", fileName));
        var cwd = Directory.GetCurrentDirectory();
        yield return Path.Combine(cwd, "apps", "avalonia", "SceneLab", "samples", fileName);
        yield return Path.Combine(cwd, "samples", fileName);
        yield return Path.Combine(@"D:\novolis\novolis-lab\labs\avalonia\SceneLab\samples", fileName);
    }
}
