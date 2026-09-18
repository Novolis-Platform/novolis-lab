using System.Text.RegularExpressions;
using ChannelHost.Contracts;
using ChannelHost.Hubs;
using ChannelHost.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Novolis.Game.Identity;
using Novolis.Game.Identity.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://127.0.0.1:5177");

var tokenService = new TokenService(builder.Configuration);
builder.Services.AddSingleton(tokenService);
builder.Services.AddSingleton<IPlayerDirectory, InMemoryPlayerDirectory>();
builder.Services.AddSingleton<ChannelDirectory>();
builder.Services.AddSingleton<SqliteMessageStore>();

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
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true));
});

var app = builder.Build();

app.UseCors();
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

app.MapHub<ChannelHub>("/hubs/channel");

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

public partial class Program;
