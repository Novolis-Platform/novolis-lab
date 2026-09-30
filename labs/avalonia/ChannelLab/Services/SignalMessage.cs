using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Novolis.Chat.Abstractions;
using Novolis.Chat.Directory;
using Novolis.Chat.Hosting.AspNetCore;
using Novolis.Messaging.SecureText;
using Novolis.Security.SecureText;

namespace ChannelLab.Services;

internal sealed record SignalMessage(
    string Channel,
    string FromNick,
    string Kind,
    string Payload,
    string? ToNick,
    string? Conversation);
