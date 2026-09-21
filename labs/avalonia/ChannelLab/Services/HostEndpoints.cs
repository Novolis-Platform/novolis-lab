namespace ChannelLab.Services;

internal static class HostEndpoints
{
    public const int Port = 5177;
    public static readonly Uri BaseUri = ResolveBaseUri();
    public static bool IsLocalHost =>
        BaseUri.IsLoopback
        || string.Equals(BaseUri.Host, "localhost", StringComparison.OrdinalIgnoreCase);

    public static readonly Uri HealthUri = new(BaseUri, "health");
    public static readonly Uri GuestUri = new(BaseUri, "api/guest");
    public static readonly Uri HubUri = new(BaseUri, "hubs/channel");

    static Uri ResolveBaseUri()
    {
        var configured = Environment.GetEnvironmentVariable("CHANNEL_HOST_URL");
        var value = string.IsNullOrWhiteSpace(configured)
            ? $"http://127.0.0.1:{Port}/"
            : configured.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "CHANNEL_HOST_URL must be an absolute HTTP or HTTPS URL.");
        }

        return new Uri(uri.ToString().EndsWith("/", StringComparison.Ordinal)
            ? uri.ToString()
            : uri + "/");
    }
}
