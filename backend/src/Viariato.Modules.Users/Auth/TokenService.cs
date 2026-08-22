using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Options;

namespace Viariato.Modules.Users.Auth;

public sealed record IssuedTokens(string AccessToken, int ExpiresIn, string RawRefreshToken, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

public enum RefreshOutcome
{
    Success,
    NotFound,
    Expired,
    Reused,
}

public sealed record RefreshResult(RefreshOutcome Outcome, RefreshToken? Token);

public sealed class TokenService(AppDbContext db, IOptions<JwtOptions> jwtOptions, IOptions<AuthOptions> authOptions)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly AuthOptions _auth = authOptions.Value;

    public async Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> GetRolesAndPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var roleIds = await db.Set<UserRole>()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

        var roles = await db.Set<Role>()
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Name)
            .ToListAsync(ct);

        var permissions = await db.Set<RolePermission>()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync(ct);

        return (roles, permissions);
    }

    public string CreateAccessToken(User user, IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        claims.AddRange(permissions.Select(permission => new Claim("perm", permission)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_jwt.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<IssuedTokens> IssueAsync(User user, string? ip, string? userAgent, CancellationToken ct)
    {
        var (roles, permissions) = await GetRolesAndPermissionsAsync(user.Id, ct);
        var accessToken = CreateAccessToken(user, roles, permissions);
        var rawRefreshToken = await CreateRefreshTokenAsync(user.Id, Guid.CreateVersion7(), ip, userAgent, ct);

        return new IssuedTokens(accessToken, _jwt.AccessTokenMinutes * 60, rawRefreshToken, roles, permissions);
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId, Guid familyId, string? ip, string? userAgent, CancellationToken ct)
    {
        var rawToken = GenerateRawToken();
        var now = DateTimeOffset.UtcNow;

        db.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(rawToken),
            FamilyId = familyId,
            ExpiresAt = now.AddDays(_auth.RefreshTokenDays),
            CreatedAt = now,
            CreatedByIp = ip,
            UserAgent = userAgent,
        });

        await db.SaveChangesAsync(ct);
        return rawToken;
    }

    public async Task<RefreshResult> ValidateAndConsumeAsync(string rawRefreshToken, CancellationToken ct)
    {
        var tokenHash = HashToken(rawRefreshToken);
        var token = await db.Set<RefreshToken>().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (token is null)
        {
            return new RefreshResult(RefreshOutcome.NotFound, null);
        }

        if (token.RevokedAt is not null)
        {
            await RevokeFamilyAsync(token.FamilyId, ct);
            return new RefreshResult(RefreshOutcome.Reused, token);
        }

        if (token.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return new RefreshResult(RefreshOutcome.Expired, token);
        }

        return new RefreshResult(RefreshOutcome.Success, token);
    }

    public async Task<IssuedTokens> RotateAsync(RefreshToken current, string? ip, string? userAgent, CancellationToken ct)
    {
        var user = await db.Set<User>().SingleAsync(u => u.Id == current.UserId, ct);

        current.RevokedAt = DateTimeOffset.UtcNow;

        var rawToken = GenerateRawToken();
        var now = DateTimeOffset.UtcNow;
        var next = new RefreshToken
        {
            UserId = current.UserId,
            TokenHash = HashToken(rawToken),
            FamilyId = current.FamilyId,
            ExpiresAt = now.AddDays(_auth.RefreshTokenDays),
            CreatedAt = now,
            CreatedByIp = ip,
            UserAgent = userAgent,
        };
        db.Add(next);
        await db.SaveChangesAsync(ct);

        current.ReplacedById = next.Id;
        await db.SaveChangesAsync(ct);

        var (roles, permissions) = await GetRolesAndPermissionsAsync(user.Id, ct);
        var accessToken = CreateAccessToken(user, roles, permissions);

        return new IssuedTokens(accessToken, _jwt.AccessTokenMinutes * 60, rawToken, roles, permissions);
    }

    public async Task<RefreshToken?> FindByRawTokenAsync(string rawToken, CancellationToken ct)
    {
        var tokenHash = HashToken(rawToken);
        return await db.Set<RefreshToken>().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
    }

    public async Task RevokeFamilyAsync(Guid familyId, CancellationToken ct)
    {
        var tokens = await db.Set<RefreshToken>()
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct)
    {
        var tokens = await db.Set<RefreshToken>()
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    private static string GenerateRawToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
