using System.Globalization;
using Novolis.Transports.Ed2k;
using Spectre.Console;

namespace Novolis.Lab.Ed2kSearch;

internal static class Ed2kSearchUi
{
    public static bool CanUseInteractiveConsole =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public static Ed2kSearchOptions PromptMissing(Ed2kSearchOptions options)
    {
        var serverPath = options.ServerPath;
        if (string.IsNullOrWhiteSpace(serverPath))
        {
            serverPath = AnsiConsole.Prompt(
                new TextPrompt<string>("Server-list cache path:")
                    .DefaultValue(Ed2kSearchOptions.DefaultServerPath));
        }

        var query = options.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            query = AnsiConsole.Prompt(
                new TextPrompt<string>("Search terms:"));
        }

        var serverUrl = options.ServerUrl;
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            serverUrl = AnsiConsole.Prompt(
                new TextPrompt<string>("Server-list HTTPS URL:")
                    .DefaultValue(Ed2kSearchOptions.DefaultServerUrl));
        }

        return options with
        {
            ServerPath = serverPath,
            Query = query,
            ServerUrl = serverUrl
        };
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
                Markup.Escape(message),
                async _ => result = await action().ConfigureAwait(false))
            .ConfigureAwait(false);
        return result!;
    }

    public static void ShowTitle()
    {
        AnsiConsole.Write(
            new Panel(
                    "[bold cyan]eD2k Search Lab[/]\n" +
                    "[grey]Search and download-readiness preview; file transfer is disabled.[/]")
                .Header("[cyan]Novolis.Transports.Ed2k[/]")
                .RoundedBorder());
        AnsiConsole.WriteLine();
    }

    public static void ShowServerList(
        ServerList servers,
        string serverPath,
        string serverUrl)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("#");
        table.AddColumn("Name");
        table.AddColumn("Endpoint");
        table.AddColumn("Users");
        table.AddColumn("Files");

        for (var index = 0; index < servers.Servers.Count; index++)
        {
            var server = servers.Servers[index];
            table.AddRow(
                (index + 1).ToString(CultureInfo.InvariantCulture),
                EscapeOrDash(server.Name),
                Escape(server.Endpoint.ToString()),
                FormatCount(server.UserCount),
                FormatCount(server.FileCount));
        }

        var metadata = new Grid()
            .AddColumn()
            .AddColumn();
        metadata.AddRow(new Markup("[grey]Cache[/]"), new Markup(Escape(serverPath)));
        metadata.AddRow(new Markup("[grey]Download source[/]"), new Markup(Escape(serverUrl)));
        metadata.AddRow(
            new Markup("[grey]Usable endpoints[/]"),
            new Markup(servers.Servers.Count.ToString(CultureInfo.InvariantCulture)));
        AnsiConsole.Write(
            new Panel(new Rows(metadata, table))
                .Header("[cyan]Server list[/]")
                .RoundedBorder()
                .Expand());
        AnsiConsole.WriteLine();
    }

    public static void ShowConnection(Ed2kConnectionInfo connection)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Server");
        table.AddColumn("Client ID");
        table.AddColumn("Reachability");
        table.AddColumn("Assigned port");
        table.AddRow(
            Escape(connection.Server.ToString()),
            connection.ClientId.ToString(CultureInfo.InvariantCulture),
            connection.IsHighId ? "[green]HighID[/]" : "[yellow]LowID[/]",
            connection.AssignedPort.ToString(CultureInfo.InvariantCulture));

        AnsiConsole.Write(
            new Panel(table)
                .Header("[cyan]Connection[/]")
                .RoundedBorder()
                .Expand());
        AnsiConsole.WriteLine();
    }

    public static void ShowSearchSummary(
        string query,
        int total,
        int skip,
        int take,
        TimeSpan elapsed)
    {
        var first = total == 0 || skip >= total ? 0 : skip + 1;
        var last = total == 0 || skip >= total
            ? 0
            : Math.Min(skip + take, total);
        var range = first == 0 ? "no rows" : $"{first}–{last}";
        var body =
            $"[grey]Query:[/] {Escape(query)}\n" +
            $"[grey]Total results:[/] {total.ToString("N0", CultureInfo.InvariantCulture)}\n" +
            $"[grey]Visible range:[/] {range}\n" +
            $"[grey]Elapsed:[/] {elapsed.TotalMilliseconds.ToString("N0", CultureInfo.InvariantCulture)} ms";
        AnsiConsole.Write(
            new Panel(body)
                .Header("[cyan]Search summary[/]")
                .RoundedBorder());
        AnsiConsole.WriteLine();
    }

    public static void ShowResults(
        IReadOnlyList<Ed2kSearchResult> results,
        int skip)
    {
        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No results in this page.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("#");
        table.AddColumn("Name");
        table.AddColumn("Type");
        table.AddColumn("Size");
        table.AddColumn("Sources");
        table.AddColumn("File ID");
        table.AddColumn("Peer");
        table.AddColumn("Transfer");

        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            table.AddRow(
                (skip + index + 1).ToString(CultureInfo.InvariantCulture),
                Escape(Shorten(result.Name, 48)),
                EscapeOrDash(Shorten(result.Type, 18)),
                FormatBytes(result.Size),
                result.SourceCount.ToString(CultureInfo.InvariantCulture),
                Escape(result.FileId.ToString()),
                $"{result.ClientId.ToString(CultureInfo.InvariantCulture)}:{result.Port.ToString(CultureInfo.InvariantCulture)}",
                Escape(GetTransferReadiness(result)));
        }

        AnsiConsole.Write(
            new Panel(table)
                .Header("[cyan]Search results[/]")
                .RoundedBorder()
                .Expand());
        AnsiConsole.WriteLine();
    }

    public static void ShowNotice(string message) =>
        AnsiConsole.MarkupLine($"[grey]{Escape(message)}[/]");

    public static void ShowFailure(string message) =>
        AnsiConsole.MarkupLine($"[red]{Escape(message)}[/]");

    public static string GetTransferReadiness(Ed2kSearchResult result)
    {
        var isHighId = Ed2kMessages.IsHighId(result.ClientId);
        var needsMultipart = result.Size > Ed2kProtocol.PartSize;
        return (isHighId, needsMultipart) switch
        {
            (true, false) => "Ready; not started",
            (true, true) => "Multipart/resume pending",
            (false, false) => "LowID callback pending",
            _ => "LowID + multipart pending"
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes.ToString(CultureInfo.InvariantCulture)} B";

        var value = (double)bytes;
        var unit = 0;
        var units = new[] { "B", "KiB", "MiB", "GiB", "TiB" };
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value.ToString("0.##", CultureInfo.InvariantCulture)} {units[unit]}";
    }

    private static string FormatCount(uint? count) =>
        count?.ToString("N0", CultureInfo.InvariantCulture) ?? "—";

    private static string EscapeOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : Escape(value);

    private static string Escape(string value) => Markup.Escape(value);

    private static string Shorten(string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maximumLength)
            return value ?? string.Empty;

        return value[..(maximumLength - 1)] + "…";
    }
}
