using System.Globalization;
using System.Text.Json;
using Novolis.Transports.NetworkPath;
using Spectre.Console;

namespace Novolis.Lab.NetworkPath;

internal static class NetworkPathLabUi
{
    public static bool CanUseInteractiveConsole =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public static NetworkProbeTarget PromptTarget()
    {
        while (true)
        {
            var text = AnsiConsole.Prompt(
                new TextPrompt<string>("Probe target (host:port):"));
            if (NetworkProbeTarget.TryParse(text, out var target, out var error))
                return target!;

            AnsiConsole.MarkupLine($"[yellow]{Escape(error ?? "Invalid target.")}[/]");
        }
    }

    public static async Task<T> RunStatusAsync<T>(
        string message,
        Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var result = default(T);
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(
                Escape(message),
                async _ => result = await action().ConfigureAwait(false))
            .ConfigureAwait(false);
        return result!;
    }

    public static void ShowTitle()
    {
        AnsiConsole.Write(
            new Panel(
                    "[bold cyan]Network Path Lab[/]\n" +
                    "[grey]Adapter inventory and explicit TCP-connect path probes.[/]")
                .Header("[cyan]Novolis.Transports.NetworkPath[/]")
                .RoundedBorder());
        AnsiConsole.WriteLine();
    }

    public static void ShowReport(NetworkPathReport report, bool showAddresses)
    {
        ShowRoute(report, showAddresses);
        ShowAdapters(report.Adapters, showAddresses);
        ShowProbes(report);
    }

    public static void WritePlain(NetworkPathReport report, bool showAddresses)
    {
        Console.WriteLine($"Target: {report.Target}");
        Console.WriteLine(
            $"Default path: {GetDefaultAdapterName(report)}; " +
            $"source {FormatAddress(report.DefaultRoute.SourceAddress, showAddresses)}");
        if (!string.IsNullOrWhiteSpace(report.DefaultRoute.Error))
            Console.WriteLine($"Route note: {Normalize(report.DefaultRoute.Error)}");

        Console.WriteLine("Adapters:");
        foreach (var path in report.Adapters)
        {
            var address = showAddresses
                ? string.Join(",", path.Adapter.Addresses.Select(address => address.ToString()))
                : $"{path.Adapter.Addresses.Count} address(es)";
            Console.WriteLine(
                $"- {Normalize(path.Adapter.Name)}\t{path.Kind}\t" +
                $"{path.Adapter.Status}\t{address}\t" +
                $"{Normalize(string.Join("; ", path.Evidence))}");
        }

        Console.WriteLine("Probes:");
        foreach (var probe in report.Probes)
        {
            var selected = ReferenceEquals(probe, report.SelectedProbe) ? "\tselected" : string.Empty;
            Console.WriteLine(
                $"- {Normalize(probe.Path.Adapter.Name)}\t" +
                $"{probe.Statistics.Successes}/{probe.Statistics.Attempts} success\t" +
                $"loss {probe.Statistics.LossPercentage.ToString("0.##", CultureInfo.InvariantCulture)}%\t" +
                $"min {FormatDuration(probe.Statistics.Minimum)}\t" +
                $"median {FormatDuration(probe.Statistics.Median)}\t" +
                $"max {FormatDuration(probe.Statistics.Maximum)}{selected}");
        }
    }

    public static void WriteJson(NetworkPathReport report, bool showAddresses)
    {
        var payload = new
        {
            target = report.Target.ToString(),
            capturedAt = report.CapturedAt,
            defaultRoute = new
            {
                adapterId = report.DefaultRoute.AdapterId,
                adapter = GetDefaultAdapterName(report),
                resolved = report.DefaultRoute.IsResolved,
                sourceAddress = showAddresses
                    ? report.DefaultRoute.SourceAddress?.ToString()
                    : null,
                error = report.DefaultRoute.Error
            },
            adapters = report.Adapters.Select(path => new
            {
                id = path.Adapter.Id,
                name = path.Adapter.Name,
                description = path.Adapter.Description,
                interfaceType = path.Adapter.InterfaceType.ToString(),
                status = path.Adapter.Status.ToString(),
                interfaceIndex = path.Adapter.InterfaceIndex,
                kind = path.Kind.ToString(),
                tunnelCandidate = path.Adapter.IsTunnelCandidate,
                addressCount = path.Adapter.Addresses.Count,
                addresses = showAddresses
                    ? path.Adapter.Addresses.Select(address => address.ToString()).ToArray()
                    : null,
                evidence = path.Evidence
            }),
            probes = report.Probes.Select(probe => new
            {
                adapterId = probe.Path.Adapter.Id,
                adapter = probe.Path.Adapter.Name,
                kind = probe.Path.Kind.ToString(),
                attempts = probe.Statistics.Attempts,
                successes = probe.Statistics.Successes,
                failures = probe.Statistics.Failures,
                lossPercentage = probe.Statistics.LossPercentage,
                minimumMilliseconds = ToMilliseconds(probe.Statistics.Minimum),
                medianMilliseconds = ToMilliseconds(probe.Statistics.Median),
                maximumMilliseconds = ToMilliseconds(probe.Statistics.Maximum),
                samples = probe.Samples.Select(sample => new
                {
                    success = sample.Succeeded,
                    milliseconds = ToMilliseconds(sample.Duration),
                    sourceAddress = showAddresses ? sample.SourceAddress?.ToString() : null,
                    error = sample.Error
                })
            }),
            selectedAdapterId = report.SelectedProbe?.Path.Adapter.Id
        };

        Console.WriteLine(
            JsonSerializer.Serialize(
                payload,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void ShowFailure(string message) =>
        AnsiConsole.MarkupLine($"[red]{Escape(message)}[/]");

    private static void ShowRoute(NetworkPathReport report, bool showAddresses)
    {
        var route = report.DefaultRoute;
        var grid = new Grid()
            .AddColumn()
            .AddColumn();
        grid.AddRow(new Markup("[grey]Target[/]"), new Markup(Escape(route.Target.ToString())));
        grid.AddRow(
            new Markup("[grey]Default adapter[/]"),
            new Markup(Escape(GetDefaultAdapterName(report))));
        grid.AddRow(
            new Markup("[grey]Selected source[/]"),
            new Markup(Escape(FormatAddress(route.SourceAddress, showAddresses))));
        grid.AddRow(
            new Markup("[grey]Route note[/]"),
            new Markup(Escape(route.Error ?? "resolved without payload data")));

        AnsiConsole.Write(
            new Panel(grid)
                .Header("[cyan]Default egress[/]")
                .RoundedBorder()
                .Expand());
        AnsiConsole.WriteLine();
    }

    private static void ShowAdapters(
        IReadOnlyList<NetworkAdapterPath> paths,
        bool showAddresses)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Adapter");
        table.AddColumn("Type");
        table.AddColumn("Status");
        table.AddColumn("Path");
        table.AddColumn("Index");
        table.AddColumn(showAddresses ? "Addresses" : "Address count");
        table.AddColumn("Evidence");

        foreach (var path in paths)
        {
            var addresses = showAddresses
                ? string.Join(", ", path.Adapter.Addresses.Select(address => Escape(address.ToString())))
                : path.Adapter.Addresses.Count.ToString(CultureInfo.InvariantCulture);
            table.AddRow(
                Escape(path.Adapter.Name),
                Escape(path.Adapter.InterfaceType.ToString()),
                Escape(path.Adapter.Status.ToString()),
                Escape(path.Kind.ToString()),
                Escape(path.Adapter.InterfaceIndex?.ToString(CultureInfo.InvariantCulture) ?? "—"),
                addresses,
                Escape(string.Join("; ", path.Evidence)));
        }

        AnsiConsole.Write(
            new Panel(table)
                .Header("[cyan]Adapters[/]")
                .RoundedBorder()
                .Expand());
        AnsiConsole.WriteLine();
    }

    private static void ShowProbes(NetworkPathReport report)
    {
        if (report.Probes.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No probe candidates were available.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Adapter");
        table.AddColumn("Path");
        table.AddColumn("Success");
        table.AddColumn("Loss");
        table.AddColumn("Min");
        table.AddColumn("Median");
        table.AddColumn("Max");
        table.AddColumn("Selected");

        foreach (var probe in report.Probes)
        {
            table.AddRow(
                Escape(probe.Path.Adapter.Name),
                Escape(probe.Path.Kind.ToString()),
                $"{probe.Statistics.Successes}/{probe.Statistics.Attempts}",
                $"{probe.Statistics.LossPercentage.ToString("0.##", CultureInfo.InvariantCulture)}%",
                Escape(FormatDuration(probe.Statistics.Minimum)),
                Escape(FormatDuration(probe.Statistics.Median)),
                Escape(FormatDuration(probe.Statistics.Maximum)),
                ReferenceEquals(probe, report.SelectedProbe) ? "[green]yes[/]" : "—");
        }

        AnsiConsole.Write(
            new Panel(table)
                .Header("[cyan]Bound TCP probes[/]")
                .RoundedBorder()
                .Expand());
        AnsiConsole.WriteLine();
    }

    private static double? ToMilliseconds(TimeSpan? duration) =>
        duration?.TotalMilliseconds;

    private static string FormatDuration(TimeSpan? duration) =>
        duration is null
            ? "—"
            : $"{duration.Value.TotalMilliseconds.ToString("0.##", CultureInfo.InvariantCulture)} ms";

    private static string FormatAddress(
        System.Net.IPAddress? address,
        bool showAddresses) =>
        !showAddresses
            ? address is null ? "redacted" : "available"
            : address?.ToString() ?? "unresolved";

    private static string GetDefaultAdapterName(NetworkPathReport report) =>
        report.DefaultRoute.AdapterId is null
            ? "unresolved"
            : report.Adapters
                .FirstOrDefault(path =>
                    string.Equals(
                        path.Adapter.Id,
                        report.DefaultRoute.AdapterId,
                        StringComparison.Ordinal))
                ?.Adapter.Name ??
              report.DefaultRoute.AdapterId;

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "—"
            : value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');

    private static string Escape(string? value) =>
        Markup.Escape(value ?? string.Empty);
}
