using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NeighborGoods.Api.Features.Auth.Configuration;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Auth.Services;

public sealed class TokenService(
    NeighborGoodsDbContext dbContext,
    IOptions<JwtOptions> jwtOptions) : ITokenService
{
    private const string RefreshProvider = "NeighborGoodsApi";
    private const string RefreshNamePrefix = "refresh:";
    private const string TokenTypeClaim = "token_type";
    private const string AccessTokenType = "access";
    private const string RefreshTokenType = "refresh";

    private readonly JwtOptions _options = jwtOptions.Value;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public async Task<AuthTokenPair> IssueAsync(AspNetUser user, CancellationToken cancellationToken = default)
    {
        return await IssueInternalAsync(user, cancellationToken);
    }

    public async Task<AuthTokenPair?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var principal = ValidateRefreshToken(refreshToken);
        if (principal is null)
        {
            return null;
        }

        var userId = GetUserId(principal);
        var jti = GetTokenId(principal);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(jti))
        {
            return null;
        }

        var tokenEntity = await dbContext.AspNetUserTokens
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.LoginProvider == RefreshProvider &&
                x.Name == BuildRefreshName(jti), cancellationToken);
        if (tokenEntity is null)
        {
            return null;
        }

        var expectedHash = HashToken(refreshToken);
        if (!string.Equals(tokenEntity.Value, expectedHash, StringComparison.Ordinal))
        {
            return null;
        }

        var user = await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        dbContext.AspNetUserTokens.Remove(tokenEntity);
        return await IssueInternalAsync(user, cancellationToken);
    }

    public async Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var principal = ValidateRefreshToken(refreshToken, validateLifetime: false);
        if (principal is null)
        {
            return false;
        }

        var userId = GetUserId(principal);
        var jti = GetTokenId(principal);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        var tokenEntity = await dbContext.AspNetUserTokens
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.LoginProvider == RefreshProvider &&
                x.Name == BuildRefreshName(jti), cancellationToken);
        if (tokenEntity is null)
        {
            return false;
        }

        var expectedHash = HashToken(refreshToken);
        if (!string.Equals(tokenEntity.Value, expectedHash, StringComparison.Ordinal))
        {
            return false;
        }

        dbContext.AspNetUserTokens.Remove(tokenEntity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<AuthTokenPair> IssueInternalAsync(AspNetUser user, CancellationToken cancellationToken)
    {
        EnsureJwtOptions();

        var now = DateTime.UtcNow;
        var accessExpiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var refreshExpiresAt = now.AddDays(_options.RefreshTokenDays);
        var refreshJti = Guid.NewGuid().ToString("N");

        var accessToken = BuildJwtToken(user, now, accessExpiresAt, AccessTokenType, jti: null);
        var refreshToken = BuildJwtToken(user, now, refreshExpiresAt, RefreshTokenType, refreshJti);

        dbContext.AspNetUserTokens.Add(new AspNetUserToken
        {
            UserId = user.Id,
            LoginProvider = RefreshProvider,
            Name = BuildRefreshName(refreshJti),
            Value = HashToken(refreshToken)
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthTokenPair(
            accessToken,
            accessExpiresAt,
            refreshToken,
            refreshExpiresAt,
            user.Id);
    }

    private string BuildJwtToken(
        AspNetUser user,
        DateTime issuedAt,
        DateTime expiresAt,
        string tokenType,
        string? jti)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(TokenTypeClaim, tokenType)
        };

        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            claims.Add(new Claim(ClaimTypes.Name, user.UserName));
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(jti))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: credentials);

        return _tokenHandler.WriteToken(token);
    }

    private ClaimsPrincipal? ValidateRefreshToken(string refreshToken, bool validateLifetime = true)
    {
        EnsureJwtOptions();

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            var principal = _tokenHandler.ValidateToken(refreshToken, validation, out _);
            var tokenType = principal.FindFirst(TokenTypeClaim)?.Value;
            return tokenType == RefreshTokenType ? principal : null;
        }
        catch
        {
            return null;
        }
    }

    private void EnsureJwtOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Issuer) ||
            string.IsNullOrWhiteSpace(_options.Audience) ||
            string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException("Jwt settings are incomplete.");
        }
    }

    private static string BuildRefreshName(string jti) => $"{RefreshNamePrefix}{jti}";

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static string? GetUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private static string? GetTokenId(ClaimsPrincipal principal)
    {
        return principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
            ?? principal.FindFirst("jti")?.Value;
    }
}
