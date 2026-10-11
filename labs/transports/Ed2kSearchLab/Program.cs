using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Novolis.Lab.Ed2kSearch;
using Novolis.Transports.Ed2k;

return await Ed2kSearchLab.RunAsync(args);

internal static class Ed2kSearchLab
{
    private const int MaximumServerListBytes = 2 * 1024 * 1024;

    public static async Task<int> RunAsync(string[] args)
    {
        if (!Ed2kSearchOptions.TryParse(args, out var options, out var parseError))
        {
            Console.Error.WriteLine(parseError);
            Console.Error.WriteLine(Ed2kSearchOptions.Usage);
            return 2;
        }

        var useSpectre = options.Interactive && Ed2kSearchUi.CanUseInteractiveConsole;
        if (useSpectre)
        {
            Ed2kSearchUi.ShowTitle();
            options = Ed2kSearchUi.PromptMissing(options);
        }

        if (string.IsNullOrWhiteSpace(options.ServerPath) ||
            string.IsNullOrWhiteSpace(options.Query))
        {
            Console.Error.WriteLine(
                "A server-list path and query are required when interactive prompts are unavailable.");
            Console.Error.WriteLine(Ed2kSearchOptions.Usage);
            return 2;
        }

        ServerList servers;
        try
        {
            using var loadTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            async Task<ServerList> LoadAsync() =>
                await LoadServerListAsync(
                        options.ServerPath,
                        options.ServerUrl,
                        loadTimeout.Token,
                        useSpectre ? Ed2kSearchUi.ShowNotice : Console.WriteLine)
                    .ConfigureAwait(false);

            servers = useSpectre
                ? await Ed2kSearchUi.RunStatusAsync("Loading server list...", LoadAsync)
                : await LoadAsync();
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or InvalidDataException or
            HttpRequestException or OperationCanceledException)
        {
            if (useSpectre)
                Ed2kSearchUi.ShowFailure($"Could not load the server list: {exception.Message}");
            else
                Console.Error.WriteLine($"Could not load the server list: {exception.Message}");
            return 1;
        }

        if (servers.Servers.Count == 0)
        {
            if (useSpectre)
                Ed2kSearchUi.ShowFailure("The server list did not contain any usable endpoints.");
            else
                Console.Error.WriteLine("The server list did not contain any usable endpoints.");
            return 1;
        }

        if (useSpectre)
            Ed2kSearchUi.ShowServerList(servers, options.ServerPath, options.ServerUrl);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var session = new Ed2kSession(clientName: "Novolis Ed2k Search");
        Ed2kConnectionInfo? connection = null;
        foreach (var server in servers.Servers)
        {
            try
            {
                if (useSpectre)
                {
                    connection = await Ed2kSearchUi.RunStatusAsync(
                        $"Connecting to {server.Endpoint}...",
                        () => session.ConnectServerAsync(server.Endpoint, timeout.Token));
                }
                else
                {
                    Console.WriteLine($"Connecting to {server.Endpoint} ...");
                    connection = await session.ConnectServerAsync(server.Endpoint, timeout.Token);
                }

                break;
            }
            catch (Exception exception) when (
                exception is SocketException or IOException or TimeoutException or OperationCanceledException)
            {
                if (useSpectre)
                    Ed2kSearchUi.ShowNotice($"{server.Endpoint} failed: {exception.Message}");
                else
                    Console.WriteLine($"  failed: {exception.Message}");
            }
        }

        if (connection is null)
        {
            if (useSpectre)
                Ed2kSearchUi.ShowFailure("No eD2k server accepted the connection.");
            else
                Console.Error.WriteLine("No eD2k server accepted the connection.");
            return 1;
        }

        if (useSpectre)
        {
            Ed2kSearchUi.ShowConnection(connection);
        }
        else
        {
            Console.WriteLine(
                $"Connected to {connection.Server}; " +
                $"{(connection.IsHighId ? "HighID" : "LowID")} {connection.ClientId}; " +
                $"port {connection.AssignedPort}.");
        }

        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<Ed2kSearchResult> results;
        try
        {
            results = useSpectre
                ? await Ed2kSearchUi.RunStatusAsync(
                    $"Searching for {options.Query}...",
                    () => session.SearchAsync(options.Query, timeout.Token))
                : await session.SearchAsync(options.Query, timeout.Token);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or OperationCanceledException)
        {
            if (useSpectre)
                Ed2kSearchUi.ShowFailure($"The search failed: {exception.Message}");
            else
                Console.Error.WriteLine($"The search failed: {exception.Message}");
            return 1;
        }
        finally
        {
            stopwatch.Stop();
        }

        var page = results
            .Skip(options.Skip)
            .Take(options.Take)
            .ToArray();
        if (useSpectre)
        {
            Ed2kSearchUi.ShowSearchSummary(
                options.Query,
                results.Count,
                options.Skip,
                options.Take,
                stopwatch.Elapsed);
            Ed2kSearchUi.ShowResults(page, options.Skip);
        }
        else
        {
            Console.WriteLine(
                $"Search results for \"{options.Query}\": {results.Count}; " +
                $"showing {page.Length} after skip {options.Skip}.");
            foreach (var result in page)
            {
                Console.WriteLine(
                    $"{result.Name}\t{result.Size} bytes\t{result.FileId}\t" +
                    $"{result.SourceCount} sources\t{result.ClientId}:{result.Port}\t" +
                    $"{Ed2kSearchUi.GetTransferReadiness(result)}");
            }
        }

        return 0;
    }

    private static async Task<ServerList> LoadServerListAsync(
        string serverPath,
        string serverUrl,
        CancellationToken cancellationToken,
        Action<string> notice)
    {
        if (File.Exists(serverPath))
        {
            var local = ServerList.Load(serverPath);
            if (local.Servers.Count > 0)
                return local;

            notice(
                $"The local server list at {Path.GetFullPath(serverPath)} is empty; " +
                $"downloading {serverUrl}.");
        }
        else
        {
            notice(
                $"The local server list at {Path.GetFullPath(serverPath)} was not found; " +
                $"downloading {serverUrl}.");
        }

        return await DownloadServerListAsync(
                serverPath,
                serverUrl,
                cancellationToken,
                notice)
            .ConfigureAwait(false);
    }

    private static async Task<ServerList> DownloadServerListAsync(
        string serverPath,
        string serverUrl,
        CancellationToken cancellationToken,
        Action<string> notice)
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
        notice(
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
