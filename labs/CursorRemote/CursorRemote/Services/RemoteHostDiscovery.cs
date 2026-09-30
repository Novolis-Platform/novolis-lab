using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CursorRemote.Protocol;

namespace CursorRemote.Services;

public static class RemoteHostDiscovery
{
    public static async Task<IReadOnlyList<DiscoveredRemoteHost>> DiscoverAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var found = new ConcurrentDictionary<string, DiscoveredRemoteHost>(StringComparer.OrdinalIgnoreCase);
        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(900) };

        var candidates = CollectCandidateAddresses().ToArray();
        var probeTasks = candidates
            .Select(address => ProbeHttpAsync(http, address, found, cancellationToken))
            .ToList();
        probeTasks.Add(ProbeUdpAsync(found, timeout, cancellationToken));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await Task.WhenAll(probeTasks).WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Partial results are fine.
        }

        return found.Values
            .OrderBy(host => host.HostName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task ProbeHttpAsync(
        HttpClient http,
        IPAddress address,
        ConcurrentDictionary<string, DiscoveredRemoteHost> found,
        CancellationToken cancellationToken)
    {
        var endpoint = $"http://{address}:{RemoteProtocol.DefaultPort}";
        try
        {
            using var response = await http.GetAsync(
                $"{endpoint}/api/v1/hello",
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var hello = await JsonSerializer.DeserializeAsync<RemoteHelloDto>(
                    stream,
                    RemoteProtocol.JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            TryAdd(found, hello, endpoint, "port-scan");
        }
        catch
        {
            // Not a Cursor Remote host.
        }
    }

    private static async Task ProbeUdpAsync(
        ConcurrentDictionary<string, DiscoveredRemoteHost> found,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var udp = new UdpClient();
        udp.EnableBroadcast = true;
        udp.Client.ReceiveTimeout = (int)Math.Clamp(timeout.TotalMilliseconds, 200, 5000);

        var who = Encoding.UTF8.GetBytes(RemoteProtocol.DiscoveryWho);
        foreach (var target in DiscoveryBroadcastTargets())
        {
            try
            {
                await udp.SendAsync(
                        who,
                        who.Length,
                        new IPEndPoint(target, RemoteProtocol.DiscoveryPort))
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Ignore unreachable broadcasts.
            }
        }

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            try
            {
                using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                receiveCts.CancelAfter(remaining);
                var result = await udp.ReceiveAsync(receiveCts.Token).ConfigureAwait(false);
                var json = Encoding.UTF8.GetString(result.Buffer);
                var hello = JsonSerializer.Deserialize<RemoteHelloDto>(json, RemoteProtocol.JsonOptions);
                var endpoint = hello?.Endpoints.FirstOrDefault()
                    ?? $"http://{result.RemoteEndPoint.Address}:{RemoteProtocol.DefaultPort}";
                TryAdd(found, hello, endpoint, "udp");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Keep listening until timeout.
            }
        }
    }

    private static void TryAdd(
        ConcurrentDictionary<string, DiscoveredRemoteHost> found,
        RemoteHelloDto? hello,
        string endpoint,
        string source)
    {
        if (hello is null)
            return;
        if (!string.Equals(hello.AppId, RemoteProtocol.AppId, StringComparison.Ordinal))
            return;
        if (!RemoteProtocol.IsCompatible(hello.ProtocolVersion))
            return;

        var key = endpoint.TrimEnd('/');
        found[key] = new DiscoveredRemoteHost(
            hello.HostName,
            key,
            hello.ProtocolVersion,
            source);
    }

    private static IEnumerable<IPAddress> CollectCandidateAddresses()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var address in NeighborAddresses())
        {
            if (set.Add(address.ToString()))
                yield return address;
        }

        if (IPAddress.TryParse("100.106.124.1", out var known) && set.Add(known.ToString()))
            yield return known;
    }

    private static IEnumerable<IPAddress> NeighborAddresses()
    {
        foreach (var line in ReadNeighborLines())
        {
            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;
            if (!IPAddress.TryParse(parts[0], out var address))
                continue;
            if (address.AddressFamily != AddressFamily.InterNetwork)
                continue;
            if (IsTailscaleAddress(address) || IsPrivateLanAddress(address))
                yield return address;
        }

        foreach (var address in LocalUnicastAddresses())
        {
            if (IsTailscaleAddress(address) || IsPrivateLanAddress(address))
                yield return address;
        }
    }

    private static List<string> ReadNeighborLines()
    {
        var lines = new List<string>();
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "arp" : "ip",
                Arguments = OperatingSystem.IsWindows() ? "-a" : "neigh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
                return lines;
            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }
        }
        catch
        {
            // Neighbor table is optional.
        }

        return lines;
    }

    private static IEnumerable<IPAddress> LocalUnicastAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork);

    private static IEnumerable<IPAddress> DiscoveryBroadcastTargets()
    {
        yield return IPAddress.Broadcast;
        foreach (var address in LocalUnicastAddresses())
        {
            var bytes = address.GetAddressBytes();
            if (IsTailscaleAddress(address))
                yield return new IPAddress([100, 127, 255, 255]);
            else if (IsPrivateLanAddress(address))
            {
                bytes[3] = 255;
                yield return new IPAddress(bytes);
            }
        }
    }

    private static bool IsTailscaleAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127;
    }

    private static bool IsPrivateLanAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
               || (bytes[0] == 192 && bytes[1] == 168)
               || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31);
    }
}
