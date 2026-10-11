using System.Net.Sockets;
using System.Text;
using Novolis.Transports.Ed2k;

return await Ed2kSearchLab.RunAsync(args);

internal static class Ed2kSearchLab
{
    private const string DefaultServerListUrl = "https://upd.emule-security.org/server.met";
    private const int MaximumServerListBytes = 2 * 1024 * 1024;

    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryReadArguments(args, out var serverPath, out var query, out var serverUrl))
        {
            Console.Error.WriteLine(
                "Usage: Ed2kSearchLab --servers <server-list.txt|server.met> " +
                "--query <terms> [--server-url <https-url>]");
            return 2;
        }

        ServerList servers;
        try
        {
            using var loadTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            servers = await LoadServerListAsync(serverPath, serverUrl, loadTimeout.Token);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or InvalidDataException or
            HttpRequestException or OperationCanceledException)
        {
            Console.Error.WriteLine($"Could not load the server list: {exception.Message}");
            return 1;
        }

        if (servers.Servers.Count == 0)
        {
            Console.Error.WriteLine("The server list did not contain any usable endpoints.");
            return 1;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var session = new Ed2kSession(clientName: "Novolis Ed2k Search");
        Ed2kConnectionInfo? connection = null;
        foreach (var server in servers.Servers)
        {
            try
            {
                Console.WriteLine($"Connecting to {server.Endpoint} ...");
                connection = await session.ConnectServerAsync(server.Endpoint, timeout.Token);
                break;
            }
            catch (Exception exception) when (
                exception is SocketException or IOException or TimeoutException or OperationCanceledException)
            {
                Console.WriteLine($"  failed: {exception.Message}");
            }
        }

        if (connection is null)
        {
            Console.Error.WriteLine("No eD2k server accepted the connection.");
            return 1;
        }

        Console.WriteLine(
            $"Connected to {connection.Server}; " +
            $"{(connection.IsHighId ? "HighID" : "LowID")} {connection.ClientId}; " +
            $"port {connection.AssignedPort}.");
        IReadOnlyList<Ed2kSearchResult> results;
        try
        {
            results = await session.SearchAsync(query, timeout.Token);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or OperationCanceledException)
        {
            Console.Error.WriteLine($"The search failed: {exception.Message}");
            return 1;
        }

        Console.WriteLine($"Search results for \"{query}\": {results.Count}");
        foreach (var result in results)
        {
            Console.WriteLine(
                $"{result.Name}\t{result.Size} bytes\t{result.FileId}\t" +
                $"{result.SourceCount} sources\t{result.ClientId}:{result.Port}");
        }

        return 0;
    }

    private static bool TryReadArguments(
        IReadOnlyList<string> args,
        out string serverPath,
        out string query,
        out string serverUrl)
    {
        serverPath = string.Empty;
        query = string.Empty;
        serverUrl = DefaultServerListUrl;
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (args[index].Equals("--servers", StringComparison.OrdinalIgnoreCase))
                serverPath = args[index + 1];
            else if (args[index].Equals("--query", StringComparison.OrdinalIgnoreCase))
                query = args[index + 1];
            else if (args[index].Equals("--server-url", StringComparison.OrdinalIgnoreCase))
                serverUrl = args[index + 1];
        }

        return !string.IsNullOrWhiteSpace(serverPath) && !string.IsNullOrWhiteSpace(query);
    }

    private static async Task<ServerList> LoadServerListAsync(
        string serverPath,
        string serverUrl,
        CancellationToken cancellationToken)
    {
        if (File.Exists(serverPath))
        {
            var local = ServerList.Load(serverPath);
            if (local.Servers.Count > 0)
                return local;

            Console.WriteLine(
                $"The local server list at {Path.GetFullPath(serverPath)} is empty; " +
                $"downloading {serverUrl}.");
        }
        else
        {
            Console.WriteLine(
                $"The local server list at {Path.GetFullPath(serverPath)} was not found; " +
                $"downloading {serverUrl}.");
        }

        return await DownloadServerListAsync(
                serverPath,
                serverUrl,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<ServerList> DownloadServerListAsync(
        string serverPath,
        string serverUrl,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The server-list URL must be an absolute HTTPS URL.",
                nameof(serverUrl));
        }

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Novolis.Ed2kSearchLab/1.0");
        using var response = await client.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > MaximumServerListBytes)
        {
            throw new InvalidDataException(
                $"The downloaded server list exceeds {MaximumServerListBytes} bytes.");
        }

        await using var input = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            if (output.Length + read > MaximumServerListBytes)
            {
                throw new InvalidDataException(
                    $"The downloaded server list exceeds {MaximumServerListBytes} bytes.");
            }

            output.Write(buffer, 0, read);
        }

        var bytes = output.ToArray();
        var servers = ParseDownloadedServerList(bytes);
        if (servers.Servers.Count == 0)
            throw new InvalidDataException("The downloaded server list contained no usable endpoints.");

        await CacheServerListAsync(serverPath, bytes, cancellationToken).ConfigureAwait(false);
        Console.WriteLine(
            $"Downloaded {servers.Servers.Count} server endpoint(s) and cached them at " +
            $"{Path.GetFullPath(serverPath)}.");
        return servers;
    }

    private static ServerList ParseDownloadedServerList(byte[] bytes)
    {
        if (bytes.Length > 0 && bytes[0] is 0xE0 or 0x0E)
            return ServerList.ParseServerMet(bytes);

        return ServerList.ParseText(
            Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
    }

    private static async Task CacheServerListAsync(
        string serverPath,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(serverPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.download";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
