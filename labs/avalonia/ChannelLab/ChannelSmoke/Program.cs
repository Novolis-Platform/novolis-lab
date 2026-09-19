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
await using var carol = await ConnectAsync("carol");

await alice.InvokeAsync("Join", "#lobby");
await bob.InvokeAsync("Join", "#lobby");
await carol.InvokeAsync("Join", "#lobby");

var aliceIdentity = SecureTextDeviceIdentity.Create();
var bobIdentity = SecureTextDeviceIdentity.Create();
var carolIdentity = SecureTextDeviceIdentity.Create();
var aliceBundle = SecureTextPublicBundle.Create(aliceIdentity);
var bobBundle = SecureTextPublicBundle.Create(bobIdentity);
var carolBundle = SecureTextPublicBundle.Create(carolIdentity);
await alice.InvokeAsync("RegisterDevice", "#lobby", ToDto(aliceBundle));
await bob.InvokeAsync("RegisterDevice", "#lobby", ToDto(bobBundle));
await carol.InvokeAsync("RegisterDevice", "#lobby", ToDto(carolBundle));

var aliceFromRelay = await bob.InvokeAsync<DeviceBundleDto?>("GetDeviceBundle", "#lobby", "alice")
                     ?? throw new Exception("alice bundle was not returned");
var bobFromRelay = await alice.InvokeAsync<DeviceBundleDto?>("GetDeviceBundle", "#lobby", "bob")
                   ?? throw new Exception("bob bundle was not returned");
var carolFromRelay = await alice.InvokeAsync<DeviceBundleDto?>("GetDeviceBundle", "#lobby", "carol")
                     ?? throw new Exception("carol bundle was not returned");
var trustedAlice = ToBundle(aliceFromRelay);
var trustedBob = ToBundle(bobFromRelay);
var trustedCarol = ToBundle(carolFromRelay);
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

var groupId = SecureTextGroupId.New();
var groupMembers = new[]
{
    new SecureTextGroupMemberDto("alice", aliceIdentity.DeviceId),
    new SecureTextGroupMemberDto("bob", bobIdentity.DeviceId),
    new SecureTextGroupMemberDto("carol", carolIdentity.DeviceId),
};
await alice.InvokeAsync("CreateSecureTextGroup", "#lobby", groupId.Value, "crew", groupMembers);

try
{
    await alice.InvokeAsync("SendSecureTextGroup", "#lobby", groupId.Value, Array.Empty<SecureTextGroupEnvelopeInputDto>());
    throw new Exception("the relay accepted group text before every member approved.");
}
catch (Exception exception) when (exception.Message.Contains("explicitly approve", StringComparison.OrdinalIgnoreCase))
{
}

await bob.InvokeAsync("ApproveSecureTextGroup", "#lobby", groupId.Value);
await carol.InvokeAsync("ApproveSecureTextGroup", "#lobby", groupId.Value);

var aliceDevice = SecureTextDeviceId.FromGuid(aliceIdentity.DeviceId);
var bobDevice = SecureTextDeviceId.FromGuid(bobIdentity.DeviceId);
var carolDevice = SecureTextDeviceId.FromGuid(carolIdentity.DeviceId);
using var aliceToBobGroup = new SecureTextSession(
    aliceIdentity,
    aliceBundle,
    trustedBob,
    new SecureTextTrustedPeer(trustedBob),
    SecureTextConversationId.DeriveForGroupMember(groupId, aliceDevice, bobDevice));
using var bobFromAliceGroup = new SecureTextSession(
    bobIdentity,
    bobBundle,
    trustedAlice,
    new SecureTextTrustedPeer(trustedAlice),
    SecureTextConversationId.DeriveForGroupMember(groupId, bobDevice, aliceDevice));
using var aliceToCarolGroup = new SecureTextSession(
    aliceIdentity,
    aliceBundle,
    trustedCarol,
    new SecureTextTrustedPeer(trustedCarol),
    SecureTextConversationId.DeriveForGroupMember(groupId, aliceDevice, carolDevice));
using var carolFromAliceGroup = new SecureTextSession(
    carolIdentity,
    carolBundle,
    trustedAlice,
    new SecureTextTrustedPeer(trustedAlice),
    SecureTextConversationId.DeriveForGroupMember(groupId, carolDevice, aliceDevice));

var bobGroupGot = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
bob.On<SecureTextGroupRelayEnvelopeDto>("SecureTextGroup", dto =>
{
    if (dto.GroupId != groupId.Value)
        return;
    try
    {
        bobGroupGot.TrySetResult(bobFromAliceGroup.Open(SecureTextEnvelopeCodec.Deserialize(dto.Envelope)).Text);
    }
    catch (Exception exception)
    {
        bobGroupGot.TrySetException(exception);
    }
});
var carolGroupGot = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
carol.On<SecureTextGroupRelayEnvelopeDto>("SecureTextGroup", dto =>
{
    if (dto.GroupId != groupId.Value)
        return;
    try
    {
        carolGroupGot.TrySetResult(carolFromAliceGroup.Open(SecureTextEnvelopeCodec.Deserialize(dto.Envelope)).Text);
    }
    catch (Exception exception)
    {
        carolGroupGot.TrySetException(exception);
    }
});

const string groupMarker = "group-proof-世界-✓";
var bobEnvelope = aliceToBobGroup.Protect(groupMarker);
var carolEnvelope = aliceToCarolGroup.Protect(groupMarker);
var bobEncoded = SecureTextEnvelopeCodec.Serialize(bobEnvelope);
var carolEncoded = SecureTextEnvelopeCodec.Serialize(carolEnvelope);
if (bobEncoded.AsSpan().IndexOf(Encoding.UTF8.GetBytes(groupMarker)) >= 0
    || carolEncoded.AsSpan().IndexOf(Encoding.UTF8.GetBytes(groupMarker)) >= 0)
{
    throw new Exception("plaintext marker appeared in a group relay envelope.");
}

await alice.InvokeAsync(
    "SendSecureTextGroup",
    "#lobby",
    groupId.Value,
    new[]
    {
        new SecureTextGroupEnvelopeInputDto(bobIdentity.DeviceId, bobEncoded),
        new SecureTextGroupEnvelopeInputDto(carolIdentity.DeviceId, carolEncoded),
    });
if (await bobGroupGot.Task.WaitAsync(TimeSpan.FromSeconds(5)) != groupMarker
    || await carolGroupGot.Task.WaitAsync(TimeSpan.FromSeconds(5)) != groupMarker)
{
    throw new Exception("the approved group members did not decrypt the group text.");
}

var groupHistory = await bob.InvokeAsync<List<SecureTextGroupRelayEnvelopeDto>>(
    "GetSecureTextGroupHistory",
    "#lobby",
    groupId.Value);
var groupHistoryEnvelope = groupHistory.SingleOrDefault(item =>
        SecureTextEnvelopeCodec.Deserialize(item.Envelope).Header.MessageId == bobEnvelope.Header.MessageId)
    ?? throw new Exception("protected group history did not contain bob's recipient copy.");
if (bobFromAliceGroup.OpenHistory(SecureTextEnvelopeCodec.Deserialize(groupHistoryEnvelope.Envelope)).Text != groupMarker)
    throw new Exception("protected group history did not decrypt at the recipient.");

await AssertStoredGroupCiphertextAsync(bobEnvelope.Header.MessageId, groupMarker);
Console.WriteLine("group e2e text ok: explicit membership approval and ciphertext-only pairwise fan-out verified");

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

static async Task AssertStoredGroupCiphertextAsync(Guid messageId, string marker)
{
    var path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Novolis",
        "ChannelLab",
        "messages.db");
    await using var connection = new SqliteConnection($"Data Source={path}");
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT envelope FROM secure_group_messages WHERE id = $id;";
    command.Parameters.AddWithValue("$id", messageId.ToString("D"));
    var payload = (byte[]?)await command.ExecuteScalarAsync()
                  ?? throw new Exception("protected group envelope was not found in SQLite.");
    if (payload.AsSpan().IndexOf(Encoding.UTF8.GetBytes(marker)) >= 0)
        throw new Exception("plaintext marker appeared in group SQLite storage.");
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
sealed record SecureTextGroupMemberDto(string Nick, Guid DeviceId);
sealed record SecureTextGroupEnvelopeInputDto(Guid RecipientDeviceId, byte[] Envelope);
sealed record SecureTextGroupRelayEnvelopeDto(
    string Channel,
    Guid GroupId,
    string GroupName,
    string FromNick,
    string ToNick,
    byte[] Envelope);
