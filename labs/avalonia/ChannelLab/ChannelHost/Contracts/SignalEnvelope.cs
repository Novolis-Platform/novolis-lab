namespace ChannelHost.Contracts;

/// <summary>Out-of-band RTC signal relayed by ChannelHub (no media bytes).</summary>
public sealed record SignalEnvelope(
    string Channel,
    string FromNick,
    string Kind,
    string Payload,
    string? ToNick = null);
