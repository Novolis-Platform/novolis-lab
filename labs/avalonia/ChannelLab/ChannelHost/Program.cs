using System.Net;
using System.Text.RegularExpressions;
using ChannelHost.Contracts;
using ChannelHost.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Logging;
using Novolis.Chat.Hosting.AspNetCore;
using Novolis.Game.Identity;
using Novolis.Game.Identity.Abstractions;

var builder = WebApplication.CreateBuilder(args);
var urls = builder.Configuration["Urls"] ?? "http://127.0.0.1:5177";
var requiresHttps = ValidateListenUrls(urls);
builder.WebHost.UseUrls(urls);
IdentityModelEventSource.ShowPII = false;

var tokenService = new TokenService(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(tokenService);
builder.Services.AddSingleton<IPlayerDirectory, InMemoryPlayerDirectory>();
builder.Services.AddSingleton<IChatHistoryStore, SqliteMessageStore>();
builder.Services.AddChatHost();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = tokenService.CreateValidationParameters();
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/channel"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();
if (requiresHttps)
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "channel-host" }));

app.MapPost("/api/guest", (GuestLoginRequest request, IPlayerDirectory directory, TokenService tokens) =>
{
    var nick = NormalizeNick(request.Nick);
    if (nick is null)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["nick"] = ["Nick must be 2–24 chars: letters, digits, underscore, hyphen."],
        });

    var player = PlayerRefFactory.CreateGuest(directory, nick);
    var (token, expires) = tokens.CreateAccessToken(player, nick);
    return Results.Ok(new GuestLoginResponse(token, nick, player.Value, expires));
});

app.MapChatHub("/hubs/channel");

app.Run();

static string? NormalizeNick(string? nick)
{
    nick = nick?.Trim() ?? string.Empty;
    if (nick.Length is < 2 or > 24)
        return null;
    if (!Regex.IsMatch(nick, "^[A-Za-z0-9_-]+$"))
        return null;
    return nick;
}

static bool ValidateListenUrls(string urls)
{
    var requiresHttps = false;
    foreach (var value in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"Urls contains an invalid absolute URI: '{value}'.");
        if (uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("ChannelHost supports only HTTP or HTTPS listen URLs.");

        var loopback = uri.IsLoopback
                       || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        if (!loopback && !IsLanListenAddress(uri.Host))
        {
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Non-loopback ChannelHost URLs must use HTTPS.");
            requiresHttps = true;
        }
        else if (!loopback
                 && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            requiresHttps = true;
        }
    }

    return requiresHttps;
}

static bool IsLanListenAddress(string host)
{
    if (string.Equals(host, "0.0.0.0", StringComparison.Ordinal)
        || string.Equals(host, "::", StringComparison.Ordinal))
    {
        return true;
    }

    if (!IPAddress.TryParse(host, out var address))
        return false;

    if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        return address.IsIPv6LinkLocal || address.IsIPv6UniqueLocal;

    var bytes = address.GetAddressBytes();
    return bytes.Length == 4
           && (bytes[0] == 10
               || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
               || (bytes[0] == 192 && bytes[1] == 168));
}

public partial class Program;
