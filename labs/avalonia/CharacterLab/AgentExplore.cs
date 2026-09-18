using CharacterLab.Agent;
using CharacterLab.Demo;
using Novolis.Agent.Core;

namespace CharacterLab;

/// <summary>Headless Agent Surface workout against the mocap wire driver.</summary>
internal static class AgentExplore
{
    public static int Run()
    {
        try
        {
            var driver = new MocapParadeDriver();
            var host = new CharacterLabAgentHost(driver);

            Console.WriteLine("=== agent-explore: hello ===");
            var hello = host.Hello();
            Console.WriteLine($"app={hello.AppId}; pid={hello.ProcessId}");

            Console.WriteLine("=== agent-explore: explore ===");
            var explore = host.Execute(new AgentCommand { ActionId = CharacterLabActionIds.Explore });
            if (!explore.Ok)
            {
                Console.Error.WriteLine($"explore failed: {explore.Message}");
                return 1;
            }

            Console.WriteLine(explore.Message);

            var snap = host.Snapshot();
            var clip = snap.StatusLines.GetValueOrDefault("clip") ?? "";
            var source = snap.StatusLines.GetValueOrDefault("source") ?? "";
            Console.WriteLine($"gate snapshot: clip={clip}; source={source}");

            // BVH mid-clip hold lock
            var bvh = driver.Clips.FirstOrDefault(c => c.Source == "cmu-bvh");
            if (bvh.Id is null)
            {
                Console.Error.WriteLine("agent-explore FAIL: no cmu-bvh clip");
                return 2;
            }

            driver.SelectClip(bvh.Id);
            driver.HoldMode = true;
            var mid = driver.DurationSeconds * 0.5f;
            host.Execute(new AgentCommand
            {
                ActionId = CharacterLabActionIds.SetPhaseTime,
                Params =
                {
                    ["clip"] = bvh.Id,
                    ["time"] = mid.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
            });
            var holds = host.Execute(new AgentCommand { ActionId = CharacterLabActionIds.SampleHolds });
            Console.WriteLine($"gate mocap holds: {holds.Message}");
            var rErr = ParseFloat(holds.Message, "rErr=");
            var lErr = ParseFloat(holds.Message, "lErr=");
            if (rErr > 0.06f || lErr > 0.08f)
            {
                Console.Error.WriteLine($"agent-explore FAIL: mocap hold lock r={rErr:0.####} l={lErr:0.####}");
                return 7;
            }

            host.Execute(new AgentCommand
            {
                ActionId = CharacterLabActionIds.SetPhaseTime,
                Params = { ["clip"] = "synthetic-drill", ["phase"] = "present" },
            });
            holds = host.Execute(new AgentCommand { ActionId = CharacterLabActionIds.SampleHolds });
            Console.WriteLine($"gate synthetic holds: {holds.Message}");
            rErr = ParseFloat(holds.Message, "rErr=");
            lErr = ParseFloat(holds.Message, "lErr=");
            if (rErr > 0.06f || lErr > 0.08f)
            {
                Console.Error.WriteLine($"agent-explore FAIL: synthetic hold lock r={rErr:0.####} l={lErr:0.####}");
                return 8;
            }

            driver.SelectClip(bvh.Id);
            var early = driver.DurationSeconds * 0.1f;
            var late = driver.DurationSeconds * 0.55f;
            var bones = host.Execute(new AgentCommand
            {
                ActionId = CharacterLabActionIds.MeasureBoneTravel,
                Params =
                {
                    ["clip"] = bvh.Id,
                    ["timeA"] = early.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["timeB"] = late.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
            });
            Console.WriteLine($"gate bones: {bones.Message}");
            var rHand = ParseFloat(bones.Message, "rHand=");
            var rFoot = ParseFloat(bones.Message, "rFoot=");
            var hips = ParseFloat(bones.Message, "hips=");
            if (rHand < 0.05f && rFoot < 0.05f && hips < 0.05f)
            {
                Console.Error.WriteLine($"agent-explore FAIL: mocap travel too small rHand={rHand:0.####} rFoot={rFoot:0.####} hips={hips:0.####}");
                return 3;
            }

            Console.WriteLine("agent-explore: OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"agent-explore: {ex}");
            return 10;
        }
    }

    static float ParseFloat(string message, string key)
    {
        var i = message.IndexOf(key, StringComparison.Ordinal);
        if (i < 0)
            return 0f;
        var start = i + key.Length;
        var end = start;
        while (end < message.Length)
        {
            var c = message[end];
            if (char.IsDigit(c) || c is '.' or '-' or 'e' or 'E' or '+' or ',')
            {
                end++;
                continue;
            }

            break;
        }

        var raw = message[start..end].Replace(',', '.');
        return float.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v
            : 0f;
    }
}
