using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

const string baseUrl = "http://127.0.0.1:5177";
var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
using var health = await http.GetAsync("/health");
health.EnsureSuccessStatusCode();
Console.WriteLine("health ok");

await using var alice = await ConnectAsync("alice");
await using var bob = await ConnectAsync("bob");

var bobGot = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
bob.On<JsonElement>("Message", dto =>
{
    var nick = dto.GetProperty("nick").GetString();
    var body = dto.GetProperty("body").GetString();
    if (nick == "alice")
        bobGot.TrySetResult(body ?? string.Empty);
});

var bobJoin = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var bobOffer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
bob.On<JsonElement>("Signal", dto =>
{
    if (dto.GetProperty("fromNick").GetString() != "alice")
        return;
    var kind = dto.GetProperty("kind").GetString();
    if (string.Equals(kind, "video-join", StringComparison.OrdinalIgnoreCase))
        bobJoin.TrySetResult();
    if (string.Equals(kind, "offer", StringComparison.OrdinalIgnoreCase))
        bobOffer.TrySetResult(dto.GetProperty("payload").GetString() ?? string.Empty);
});

await alice.InvokeAsync("Join", "#lobby");
await bob.InvokeAsync("Join", "#lobby");
await Task.Delay(200);

const string marker = "fan-out-proof-" + "ok";
await alice.InvokeAsync("Say", "#lobby", marker);

var received = await bobGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
if (!string.Equals(received, marker, StringComparison.Ordinal))
    throw new Exception($"Expected '{marker}', got '{received}'.");

Console.WriteLine("fan-out ok: bob received alice message");

await alice.InvokeAsync("Signal", "#lobby", "video-join", string.Empty, null);
await bobJoin.Task.WaitAsync(TimeSpan.FromSeconds(5));
Console.WriteLine("signaling ok: bob received video-join");

await alice.InvokeAsync("Signal", "#lobby", "offer", "v=fake-sdp-offer", "bob");
var payload = await bobOffer.Task.WaitAsync(TimeSpan.FromSeconds(5));
if (!string.Equals(payload, "v=fake-sdp-offer", StringComparison.Ordinal))
    throw new Exception($"Expected fake SDP payload, got '{payload}'.");

Console.WriteLine("signaling ok: bob received alice offer fan-out");
return 0;

async Task<HubConnection> ConnectAsync(string nick)
{
    using var response = await http.PostAsJsonAsync("/api/guest", new { nick });
    response.EnsureSuccessStatusCode();
    var guest = await response.Content.ReadFromJsonAsync<Guest>(json)
                ?? throw new Exception("guest failed");

    var hub = new HubConnectionBuilder()
        .WithUrl($"{baseUrl}/hubs/channel", o => o.AccessTokenProvider = () => Task.FromResult(guest.AccessToken)!)
        .Build();
    await hub.StartAsync();
    Console.WriteLine($"connected {nick}");
    return hub;
}

sealed record Guest(string AccessToken, string Nick, Guid PlayerId, DateTimeOffset ExpiresAtUtc);
