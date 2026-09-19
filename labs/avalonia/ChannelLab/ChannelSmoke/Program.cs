using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Novolis.Messaging.SecureText;
using Novolis.Security.SecureText;

const string baseUrl = "http://127.0.0.1:5177";
var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
using var health = await http.GetAsync("/health");
health.EnsureSuccessStatusCode();
Console.WriteLine("health ok");

await using var alice = await ConnectAsync("alice");
await using var bob = await ConnectAsync("bob");

await alice.InvokeAsync("Join", "#lobby");
await bob.InvokeAsync("Join", "#lobby");

var aliceIdentity = SecureTextDeviceIdentity.Create();
var bobIdentity = SecureTextDeviceIdentity.Create();
var aliceBundle = SecureTextPublicBundle.Create(aliceIdentity);
var bobBundle = SecureTextPublicBundle.Create(bobIdentity);
await alice.InvokeAsync("RegisterDevice", "#lobby", ToDto(aliceBundle));
await bob.InvokeAsync("RegisterDevice", "#lobby", ToDto(bobBundle));

var aliceFromRelay = await bob.InvokeAsync<DeviceBundleDto?>("GetDeviceBundle", "#lobby", "alice")
                     ?? throw new Exception("alice bundle was not returned");
var bobFromRelay = await alice.InvokeAsync<DeviceBundleDto?>("GetDeviceBundle", "#lobby", "bob")
                   ?? throw new Exception("bob bundle was not returned");
var trustedAlice = ToBundle(aliceFromRelay);
var trustedBob = ToBundle(bobFromRelay);
var conversation = SecureTextConversationId.DeriveForPair(
    SecureTextDeviceId.FromGuid(aliceIdentity.DeviceId),
    SecureTextDeviceId.FromGuid(bobIdentity.DeviceId));

using var aliceSession = new SecureTextSession(
    aliceIdentity,
    aliceBundle,
    trustedBob,
    new SecureTextTrustedPeer(trustedBob),
    conversation);
using var bobSession = new SecureTextSession(
    bobIdentity,
    bobBundle,
    trustedAlice,
    new SecureTextTrustedPeer(trustedAlice),
    conversation);

var bobGot = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
bob.On<SecureTextRelayEnvelopeDto>("SecureText", dto =>
{
    try
    {
        var envelope = SecureTextEnvelopeCodec.Deserialize(dto.Envelope);
        var message = bobSession.Open(envelope);
        bobGot.TrySetResult(message.Text);
    }
    catch (Exception exception)
    {
        bobGot.TrySetException(exception);
    }
});

var bobOffer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
bob.On<JsonElement>("Signal", dto =>
{
    if (dto.GetProperty("fromNick").GetString() != "alice")
        return;
    var kind = dto.GetProperty("kind").GetString();
    if (string.Equals(kind, "offer", StringComparison.OrdinalIgnoreCase))
        bobOffer.TrySetResult(dto.GetProperty("payload").GetString() ?? string.Empty);
});

const string marker = "e2e-proof-世界-✓";
var protectedEnvelope = aliceSession.Protect(marker);
var encodedEnvelope = SecureTextEnvelopeCodec.Serialize(protectedEnvelope);
if (encodedEnvelope.AsSpan().IndexOf(Encoding.UTF8.GetBytes(marker)) >= 0)
    throw new Exception("plaintext marker appeared in the relay envelope.");
await alice.InvokeAsync("SendSecureText", "#lobby", "bob", encodedEnvelope);

var received = await bobGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
if (!string.Equals(received, marker, StringComparison.Ordinal))
    throw new Exception($"Expected '{marker}', got '{received}'.");

var history = await bob.InvokeAsync<List<SecureTextRelayEnvelopeDto>>("GetSecureHistory", "#lobby");
var storedEnvelope = history.SingleOrDefault(item =>
        SecureTextEnvelopeCodec.Deserialize(item.Envelope).Header.MessageId == protectedEnvelope.Header.MessageId)
    ?? throw new Exception("protected history did not contain the sent envelope.");
var fromHistory = bobSession.OpenHistory(SecureTextEnvelopeCodec.Deserialize(storedEnvelope.Envelope));
if (!string.Equals(fromHistory.Text, marker, StringComparison.Ordinal))
    throw new Exception("protected history did not decrypt at the endpoint.");

var tamperedCiphertext = protectedEnvelope.ExportCiphertext();
tamperedCiphertext[0] ^= 0x01;
var tamperedEnvelope = new SecureTextEnvelope(
    protectedEnvelope.Header,
    protectedEnvelope.ExportNonce(),
    tamperedCiphertext,
    protectedEnvelope.ExportAuthenticationTag());
try
{
    bobSession.Open(tamperedEnvelope);
    throw new Exception("tampered ciphertext was accepted.");
}
catch (CryptographicException)
{
}

await AssertStoredCiphertextAsync(protectedEnvelope.Header.MessageId, marker);
Console.WriteLine("e2e text ok: recipient decrypted the marker and relay storage held ciphertext only");

await alice.InvokeAsync("Signal", "#lobby", "video-join", string.Empty, null);
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

static DeviceBundleDto ToDto(SecureTextPublicBundle bundle) =>
    new(
        bundle.ProtocolVersion,
        bundle.DeviceId,
        bundle.ExportSigningPublicKey(),
        bundle.ExportAgreementPublicKey(),
        bundle.IssuedAtUtc,
        bundle.ExpiresAtUtc,
        bundle.ExportSignature());

static SecureTextPublicBundle ToBundle(DeviceBundleDto dto) =>
    new(
        dto.ProtocolVersion,
        dto.DeviceId,
        dto.SigningPublicKey,
        dto.AgreementPublicKey,
        dto.IssuedAtUtc,
        dto.ExpiresAtUtc,
        dto.Signature);

static async Task AssertStoredCiphertextAsync(Guid messageId, string marker)
{
    var path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Novolis",
        "ChannelLab",
        "messages.db");
    await using var connection = new SqliteConnection($"Data Source={path}");
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT envelope FROM secure_messages WHERE id = $id;";
    command.Parameters.AddWithValue("$id", messageId.ToString("D"));
    var payload = (byte[]?)await command.ExecuteScalarAsync()
                  ?? throw new Exception("protected envelope was not found in SQLite.");
    if (payload.AsSpan().IndexOf(Encoding.UTF8.GetBytes(marker)) >= 0)
        throw new Exception("plaintext marker appeared in SQLite.");
}

sealed record Guest(string AccessToken, string Nick, Guid PlayerId, DateTimeOffset ExpiresAtUtc);
sealed record DeviceBundleDto(
    int ProtocolVersion,
    Guid DeviceId,
    byte[] SigningPublicKey,
    byte[] AgreementPublicKey,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    byte[] Signature);
sealed record SecureTextRelayEnvelopeDto(string Channel, string FromNick, string ToNick, byte[] Envelope);
