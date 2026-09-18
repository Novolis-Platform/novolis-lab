using KatoriLab.Agent;
using KatoriLab.Demo;
using Novolis.Agent.Core;

namespace KatoriLab;

/// <summary>Headless agent-host explore gate (no HTTP listener).</summary>
internal static class AgentExplore
{
    public static int Run()
    {
        try
        {
            var driver = new KatoriKataDriver { HoldMode = true };
            var host = new KatoriLabAgentHost(driver);

            var explore = host.Execute(new AgentCommand { ActionId = KatoriLabActionIds.Explore });
            Console.WriteLine($"gate explore: {explore.Message}");
            if (!explore.Ok)
            {
                Console.Error.WriteLine("agent-explore FAIL: explore action.");
                return 1;
            }

            var holds = host.Execute(new AgentCommand
            {
                ActionId = KatoriLabActionIds.SampleHolds,
                Params = { ["phase"] = "chudan" },
            });
            Console.WriteLine($"gate chudan holds: {holds.Message}");
            if (!holds.Ok || !holds.Message.Contains("rErr=", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("agent-explore FAIL: sampleholds.");
                return 2;
            }

            if (!TryParseErr(holds.Message, "rErr", out var rErr) ||
                !TryParseErr(holds.Message, "lErr", out var lErr))
            {
                Console.Error.WriteLine("agent-explore FAIL: could not parse hold errors.");
                return 3;
            }

            if (rErr > 0.08f || lErr > 0.10f)
            {
                Console.Error.WriteLine($"agent-explore FAIL: hold lock r={rErr:0.####} l={lErr:0.####}");
                return 4;
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

    static bool TryParseErr(string message, string key, out float value)
    {
        value = float.MaxValue;
        var marker = key + "=";
        var i = message.IndexOf(marker, StringComparison.Ordinal);
        if (i < 0)
            return false;
        i += marker.Length;
        var end = i;
        while (end < message.Length &&
               (char.IsDigit(message[end]) || message[end] is '.' or ',' or '-' or 'E' or 'e'))
            end++;
        var span = message.AsSpan(i, end - i).ToString().Replace(',', '.');
        return float.TryParse(span, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
