using System.Globalization;

namespace Novolis.Lab.Ed2kSearch;

internal sealed record Ed2kSearchOptions(
    string? ServerPath,
    string? Query,
    string ServerUrl,
    int Skip,
    int Take,
    bool Interactive)
{
    public const int DefaultTake = 25;
    public const int MaximumTake = 1_000;
    public const string DefaultServerUrl = "https://upd.emule-security.org/server.met";

    public static string DefaultServerPath
    {
        get
        {
            var localApplicationData =
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(localApplicationData)
                ? Path.Combine(AppContext.BaseDirectory, "servers.met")
                : Path.Combine(
                    localApplicationData,
                    "Novolis",
                    "Ed2kSearchLab",
                    "servers.met");
        }
    }

    public static string Usage =>
        "Usage: Ed2kSearchLab --servers <server-list.txt|server.met> " +
        "--query <terms> [--server-url <https-url>] " +
        "[--skip <count>] [--take <count>] [--interactive]";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out Ed2kSearchOptions options,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? serverPath = null;
        string? query = null;
        var serverUrl = DefaultServerUrl;
        var skip = 0;
        var take = DefaultTake;
        var interactive = false;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument.ToLowerInvariant())
            {
                case "--servers":
                    if (!TryReadValue(args, ref index, out serverPath))
                    {
                        options = default!;
                        error = "--servers requires a path.";
                        return false;
                    }

                    break;
                case "--query":
                    if (!TryReadValue(args, ref index, out query))
                    {
                        options = default!;
                        error = "--query requires search terms.";
                        return false;
                    }

                    break;
                case "--server-url":
                    if (!TryReadValue(args, ref index, out serverUrl))
                    {
                        options = default!;
                        error = "--server-url requires an HTTPS URL.";
                        return false;
                    }

                    break;
                case "--skip":
                    if (!TryReadInt(args, ref index, out skip) || skip < 0)
                    {
                        options = default!;
                        error = "--skip must be a non-negative integer.";
                        return false;
                    }

                    break;
                case "--take":
                    if (!TryReadInt(args, ref index, out take) ||
                        take < 1 ||
                        take > MaximumTake)
                    {
                        options = default!;
                        error = $"--take must be between 1 and {MaximumTake}.";
                        return false;
                    }

                    break;
                case "--interactive":
                    interactive = true;
                    break;
                default:
                    options = default!;
                    error = $"Unknown option '{argument}'.";
                    return false;
            }
        }

        if (!interactive &&
            (string.IsNullOrWhiteSpace(serverPath) || string.IsNullOrWhiteSpace(query)))
        {
            options = default!;
            error = "--servers and --query are required unless --interactive is used.";
            return false;
        }

        serverUrl ??= DefaultServerUrl;
        options = new Ed2kSearchOptions(
            serverPath,
            query,
            serverUrl,
            skip,
            take,
            interactive);
        error = null;
        return true;
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        out string? value)
    {
        value = null;
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            return false;

        value = args[++index];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadInt(
        IReadOnlyList<string> args,
        ref int index,
        out int value)
    {
        value = 0;
        return TryReadValue(args, ref index, out var text) &&
               int.TryParse(
                   text,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out value);
    }
}
