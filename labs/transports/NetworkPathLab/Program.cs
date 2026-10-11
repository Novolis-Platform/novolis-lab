using System.Net.Sockets;
using Novolis.Lab.NetworkPath;
using Novolis.Transports.NetworkPath;

return await NetworkPathLab.RunAsync(args);

internal static class NetworkPathLab
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Any(argument =>
                argument.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                argument.Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine(NetworkPathLabOptions.Usage);
            return 0;
        }

        if (!NetworkPathLabOptions.TryParse(args, out var options, out var parseError))
        {
            Console.Error.WriteLine(parseError);
            Console.Error.WriteLine(NetworkPathLabOptions.Usage);
            return 2;
        }

        var canPrompt = options.Interactive && NetworkPathLabUi.CanUseInteractiveConsole;
        if (canPrompt)
        {
            if (options.Format == NetworkPathOutputFormat.Table)
                NetworkPathLabUi.ShowTitle();
            if (options.Target is null)
                options = options with { Target = NetworkPathLabUi.PromptTarget() };
        }

        if (options.Target is null)
        {
            Console.Error.WriteLine(
                "An explicit --target host:port is required when interactive prompts are unavailable.");
            Console.Error.WriteLine(NetworkPathLabOptions.Usage);
            return 2;
        }

        var request = new NetworkProbeRequest(
            options.Target,
            options.Count,
            options.Timeout,
            options.AllAdapters);
        var inspector = new NetworkPathInspector();
        try
        {
            var report = canPrompt && options.Format == NetworkPathOutputFormat.Table
                ? await NetworkPathLabUi.RunStatusAsync(
                    $"Inspecting {options.Target}...",
                    () => inspector.InspectAsync(request))
                : await inspector.InspectAsync(request);

            if (options.Format == NetworkPathOutputFormat.Json)
                NetworkPathLabUi.WriteJson(report, options.ShowAddresses);
            else if (canPrompt)
                NetworkPathLabUi.ShowReport(report, options.ShowAddresses);
            else
                NetworkPathLabUi.WritePlain(report, options.ShowAddresses);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("The network path inspection was cancelled.");
            return 1;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or SocketException)
        {
            Console.Error.WriteLine($"The network path inspection failed: {exception.Message}");
            return 1;
        }
    }
}
