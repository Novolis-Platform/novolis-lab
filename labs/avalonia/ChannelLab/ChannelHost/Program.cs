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
        if (!loopback)
        {
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Non-loopback ChannelHost URLs must use HTTPS.");
            requiresHttps = true;
        }
    }

    return requiresHttps;
}

public partial class Program;
