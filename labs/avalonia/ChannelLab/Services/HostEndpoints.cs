namespace ChannelLab.Services;

internal static class HostEndpoints
{
    public const int Port = 5177;
    public static readonly Uri BaseUri = new($"http://127.0.0.1:{Port}/");
    public static readonly Uri HealthUri = new(BaseUri, "health");
    public static readonly Uri GuestUri = new(BaseUri, "api/guest");
    public static readonly Uri HubUri = new(BaseUri, "hubs/channel");
}
