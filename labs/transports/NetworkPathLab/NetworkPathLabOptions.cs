using System.Globalization;
using Novolis.Transports.NetworkPath;

namespace Novolis.Lab.NetworkPath;

internal sealed record NetworkPathLabOptions(
    NetworkProbeTarget? Target,
    int Count,
    TimeSpan Timeout,
    bool AllAdapters,
    bool Interactive,
    NetworkPathOutputFormat Format,
    bool ShowAddresses)
{
    public const string Usage =
        "Usage: NetworkPathLab --target <host:port> " +
        "[--count <1-100>] [--timeout <seconds>] [--all-adapters] " +
        "[--format table|json] [--show-addresses] [--interactive]";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out NetworkPathLabOptions options,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? targetText = null;
        var count = 3;
        var timeout = TimeSpan.FromSeconds(3);
        var allAdapters = false;
        var interactive = false;
        var format = NetworkPathOutputFormat.Table;
        var showAddresses = false;

        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index].ToLowerInvariant())
            {
                case "--target":
                    if (!TryReadValue(args, ref index, out targetText))
                    {
                        options = default!;
                        error = "--target requires a host:port value.";
                        return false;
                    }

                    break;
                case "--count":
                    if (!TryReadInteger(args, ref index, out count) ||
                        count is < 1 or > 100)
                    {
                        options = default!;
                        error = "--count must be between 1 and 100.";
                        return false;
                    }

                    break;
                case "--timeout":
                    if (!TryReadValue(args, ref index, out var timeoutText) ||
                        !double.TryParse(
                            timeoutText,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var timeoutSeconds) ||
                        timeoutSeconds <= 0 ||
                        timeoutSeconds > 120)
                    {
                        options = default!;
                        error = "--timeout must be greater than 0 and no more than 120 seconds.";
                        return false;
                    }

                    timeout = TimeSpan.FromSeconds(timeoutSeconds);
                    break;
                case "--all-adapters":
                    allAdapters = true;
                    break;
                case "--interactive":
                    interactive = true;
                    break;
                case "--show-addresses":
                    showAddresses = true;
                    break;
                case "--format":
                    if (!TryReadValue(args, ref index, out var formatText))
                    {
                        options = default!;
                        error = "--format requires table or json.";
                        return false;
                    }

                    if (formatText.Equals("table", StringComparison.OrdinalIgnoreCase))
                        format = NetworkPathOutputFormat.Table;
                    else if (formatText.Equals("json", StringComparison.OrdinalIgnoreCase))
                        format = NetworkPathOutputFormat.Json;
                    else
                    {
                        options = default!;
                        error = "--format must be table or json.";
                        return false;
                    }

                    break;
                default:
                    options = default!;
                    error = $"Unknown option '{args[index]}'.";
                    return false;
            }
        }

        NetworkProbeTarget? target = null;
        if (targetText is not null &&
            !NetworkProbeTarget.TryParse(targetText, out target, out error))
        {
            options = default!;
            return false;
        }

        options = new NetworkPathLabOptions(
            target,
            count,
            timeout,
            allAdapters,
            interactive,
            format,
            showAddresses);
        error = null;
        return true;
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        out string value)
    {
        value = string.Empty;
        if (index + 1 >= args.Count ||
            args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        value = args[++index];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadInteger(
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
