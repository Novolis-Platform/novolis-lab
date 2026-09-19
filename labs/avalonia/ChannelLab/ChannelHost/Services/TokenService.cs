using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Novolis.Game.Identity.Abstractions;
using Novolis.Game.Identity.AspNetCore;

namespace ChannelHost.Services;

public sealed class TokenService
{
    public const string NickClaim = "nick";
    public const string Issuer = "ChannelLab-host";
    public const string Audience = "ChannelLab";
    private const string DevelopmentSigningKey = "ChannelLab-local-dev-signing-key-change-me!!";

    readonly SymmetricSecurityKey _key;
    readonly SigningCredentials _credentials;

    public TokenService(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        var configuredSecret = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            if (!environment.IsDevelopment())
                throw new InvalidOperationException("Jwt:SigningKey is required outside Development.");
            configuredSecret = DevelopmentSigningKey;
        }
        if (!environment.IsDevelopment()
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(configuredSecret),
                Encoding.UTF8.GetBytes(DevelopmentSigningKey)))
        {
            throw new InvalidOperationException("The development JWT signing key is forbidden outside Development.");
        }
        if (Encoding.UTF8.GetByteCount(configuredSecret) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");

        var secret = configuredSecret;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _credentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
    }

    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = _key,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = NickClaim,
    };

    public (string Token, DateTimeOffset ExpiresAtUtc) CreateAccessToken(PlayerRef player, string nick)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(12);
        var claims = new[]
        {
            player.ToPlayerRefClaim(),
            new Claim(NickClaim, nick),
            new Claim(ClaimTypes.Name, nick),
            new Claim(JwtRegisteredClaimNames.Sub, player.Value.ToString("D")),
        };

        var jwt = new JwtSecurityToken(
            Issuer,
            Audience,
            claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expires.UtcDateTime,
            signingCredentials: _credentials);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
