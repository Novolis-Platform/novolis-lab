using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Novolis.Chat.Abstractions;
using Novolis.Chat.Directory;
using Novolis.Chat.Hosting.AspNetCore;
using Novolis.Messaging.SecureText;
using Novolis.Security.SecureText;

var baseUrl = Environment.GetEnvironmentVariable("CHANNEL_HOST_URL")
              ?? "http://127.0.0.1:5177";
var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
using var health = await http.GetAsync("/health");
health.EnsureSuccessStatusCode();
Console.WriteLine("health ok");

await using var alice = await ConnectAsync("alice");
await using var bob = await ConnectAsync("bob");
await using var carol = await ConnectAsync("carol");
await using var dave = await ConnectAsync("dave");
await using var eve = await ConnectAsync("eve");

await alice.InvokeAsync("Join", "#lobby");
await bob.InvokeAsync("Join", "#lobby");
await carol.InvokeAsync("Join", "#lobby");
await dave.InvokeAsync("Join", "#lobby");
await eve.InvokeAsync("Join", "#lobby");

var namedChannel = await alice.InvokeAsync<ChatChannelInfo>("CreateChannel", "crew");
if (!string.Equals(namedChannel.Name, "#crew", StringComparison.Ordinal))
    throw new Exception("named channel creation did not normalize the channel name.");
await bob.InvokeAsync("Join", "#crew");
await alice.InvokeAsync("Join", "#crew");
await bob.InvokeAsync("Join", "#lobby");
await alice.InvokeAsync("Join", "#lobby");
Console.WriteLine("named channel ok: alice and bob joined #crew, then returned to #lobby");

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

var bobTyping = new TaskCompletionSource<IReadOnlyList<ChatTyping>>(
    TaskCreationOptions.RunContinuationsAsynchronously);
bob.On<List<ChatTyping>>("Typing", values =>
{
    if (values.Any(value => string.Equals(value.Nick, "alice", StringComparison.OrdinalIgnoreCase)))
        bobTyping.TrySetResult(values);
});

var firstMessageId = Guid.Empty;
var bobReceipt = new TaskCompletionSource<ChatReceipt>(
    TaskCreationOptions.RunContinuationsAsynchronously);
bob.On<ChatReceipt>("Receipt", receipt =>
{
    if (receipt.Message.Value == firstMessageId)
        bobReceipt.TrySetResult(receipt);
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

const string marker = "**e2e-proof-世界-✓**";
var protectedEnvelope = aliceSession.Protect(marker);
firstMessageId = protectedEnvelope.Header.MessageId;
var encodedEnvelope = SecureTextEnvelopeCodec.Serialize(protectedEnvelope);
if (encodedEnvelope.AsSpan().IndexOf(Encoding.UTF8.GetBytes(marker)) >= 0)
    throw new Exception("plaintext marker appeared in the relay envelope.");
await alice.InvokeAsync("SendSecureText", "#lobby", "bob", encodedEnvelope);

var received = await bobGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
if (!string.Equals(received, marker, StringComparison.Ordinal))
    throw new Exception($"Expected '{marker}', got '{received}'.");

await alice.InvokeAsync("Typing", "#lobby", true, "markdown-thread");
var typingSnapshot = await bobTyping.Task.WaitAsync(TimeSpan.FromSeconds(5));
if (!typingSnapshot.Any(value => value.Conversation == "markdown-thread"))
    throw new Exception("typing snapshot did not preserve the conversation key.");
await alice.InvokeAsync("Typing", "#lobby", false, "markdown-thread");

await bob.InvokeAsync("Receipt", "#lobby", protectedEnvelope.Header.MessageId);
var receipt = await bobReceipt.Task.WaitAsync(TimeSpan.FromSeconds(5));
if (!string.Equals(receipt.Nick, "bob", StringComparison.OrdinalIgnoreCase))
    throw new Exception("receipt did not identify the acknowledging nick.");

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

const string threadMarker = "_thread-proof_";
var threadId = ThreadId.New();
var threadEnvelope = aliceSession.Protect(threadMarker);
var threadAnnotations = new ChatFrameAnnotations(
    threadId,
    MessageRef.FromGuid(protectedEnvelope.Header.MessageId));
await alice.InvokeAsync(
    "SendSecureTextWithAnnotations",
    "#lobby",
    "bob",
    SecureTextEnvelopeCodec.Serialize(threadEnvelope),
    threadAnnotations);
var threadHistory = await bob.InvokeAsync<List<SecureTextRelayEnvelopeDto>>(
    "GetSecureHistory",
    "#lobby");
var threaded = threadHistory.SingleOrDefault(item =>
                     item.Frame.MessageId == threadEnvelope.Header.MessageId)
               ?? throw new Exception("threaded message was not found in protected history.");
if (threaded.Frame.Thread?.Value != threadId.Value
    || threaded.Frame.Parent?.Value != protectedEnvelope.Header.MessageId
    || threaded.Frame.BodyFormat != ChatBodyFormat.Markdown)
{
    throw new Exception("ChatFrame did not preserve Markdown and thread metadata beside the envelope.");
}
Console.WriteLine("thread metadata ok: ChatFrame preserved Markdown format and parent reference");

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

foreach (var peer in new[] { alice, bob, carol, dave })
{
    await peer.InvokeAsync(
        "Signal",
        "#lobby",
        "video-join",
        string.Empty,
        null,
        "call-a");
}

try
{
    await eve.InvokeAsync(
        "Signal",
        "#lobby",
        "video-join",
        string.Empty,
        null,
        "call-a");
    throw new Exception("the fifth participant joined a full conversation mesh.");
}
catch (Exception exception) when (
    exception.Message.Contains("mesh full", StringComparison.OrdinalIgnoreCase))
{
}

await eve.InvokeAsync(
    "Signal",
    "#lobby",
    "video-join",
    string.Empty,
    null,
    "call-b");
await alice.InvokeAsync("Signal", "#lobby", "offer", "v=fake-sdp-offer", "bob", "call-a");
var payload = await bobOffer.Task.WaitAsync(TimeSpan.FromSeconds(5));
if (!string.Equals(payload, "v=fake-sdp-offer", StringComparison.Ordinal))
    throw new Exception($"Expected fake SDP payload, got '{payload}'.");

Console.WriteLine("signaling ok: conversation-scoped mesh capacity and bob offer fan-out verified");
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
