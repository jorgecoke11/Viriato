using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Auth;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Options;
using Xunit;

namespace Viariato.Modules.Users.Tests.Auth;

public sealed class TokenServiceTests
{
    private static TokenService CreateService(AppDbContext db) =>
        new(
            db,
            Options.Create(new JwtOptions
            {
                Issuer = "viariato.tests",
                Audience = "viariato.tests",
                SigningKey = new string('a', 32),
                AccessTokenMinutes = 15,
            }),
            Options.Create(new AuthOptions { RefreshTokenDays = 30 }));

    private static User NewUser() => new()
    {
        Email = "user@example.com",
        EmailNormalized = "user@example.com",
        DisplayName = "Test User",
        PasswordHash = "irrelevant-for-these-tests",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void CreateAccessToken_IncludesRolesPermissionsAndSubjectClaims()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var user = NewUser();

        var token = service.CreateAccessToken(user, ["Admin"], ["users.read", "roles.manage"]);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Admin");
        Assert.Contains(jwt.Claims, c => c.Type == "perm" && c.Value == "users.read");
        Assert.Contains(jwt.Claims, c => c.Type == "perm" && c.Value == "roles.manage");
    }

    [Fact]
    public async Task IssueAsync_PersistsAHashedRefreshToken_NeverTheRawValue()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var user = NewUser();
        db.Add(user);
        await db.SaveChangesAsync();

        var issued = await service.IssueAsync(user, "127.0.0.1", "test-agent", default);

        var stored = await db.Set<RefreshToken>().SingleAsync();
        Assert.NotEqual(issued.RawRefreshToken, stored.TokenHash);
        Assert.Null(stored.RevokedAt);
        Assert.Equal(user.Id, stored.UserId);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_ReturnsNotFound_ForAnUnknownToken()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        var result = await service.ValidateAndConsumeAsync("does-not-exist", default);

        Assert.Equal(RefreshOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_ReturnsExpired_PastExpiry()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var user = NewUser();
        db.Add(user);
        await db.SaveChangesAsync();

        var issued = await service.IssueAsync(user, null, null, default);
        var stored = await db.Set<RefreshToken>().SingleAsync();
        stored.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var result = await service.ValidateAndConsumeAsync(issued.RawRefreshToken, default);

        Assert.Equal(RefreshOutcome.Expired, result.Outcome);
    }

    [Fact]
    public async Task RotateAsync_RevokesTheOldTokenAndLinksReplacedByWithinTheSameFamily()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var user = NewUser();
        db.Add(user);
        await db.SaveChangesAsync();

        await service.IssueAsync(user, null, null, default);
        var original = await db.Set<RefreshToken>().SingleAsync();
        var originalFamilyId = original.FamilyId;

        await service.RotateAsync(original, "127.0.0.1", "agent", default);

        await db.Entry(original).ReloadAsync();
        Assert.NotNull(original.RevokedAt);
        Assert.NotNull(original.ReplacedById);

        var next = await db.Set<RefreshToken>().SingleAsync(t => t.Id == original.ReplacedById);
        Assert.Equal(originalFamilyId, next.FamilyId);
        Assert.Null(next.RevokedAt);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_DetectsReuseOfARevokedToken_AndRevokesTheWholeFamily()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var user = NewUser();
        db.Add(user);
        await db.SaveChangesAsync();

        var issued = await service.IssueAsync(user, null, null, default);
        var firstToken = await db.Set<RefreshToken>().SingleAsync();

        // Rotate once — this revokes firstToken and mints a second, still-valid token in the same family.
        var rotated = await service.RotateAsync(firstToken, null, null, default);

        // Replaying the original (already-revoked) token must be treated as theft.
        var reuseResult = await service.ValidateAndConsumeAsync(issued.RawRefreshToken, default);
        Assert.Equal(RefreshOutcome.Reused, reuseResult.Outcome);

        // The whole family — including the otherwise still-valid rotated token — must now be revoked.
        var secondTokenResult = await service.ValidateAndConsumeAsync(rotated.RawRefreshToken, default);
        Assert.Equal(RefreshOutcome.Reused, secondTokenResult.Outcome);

        var allTokensInFamily = await db.Set<RefreshToken>().Where(t => t.FamilyId == firstToken.FamilyId).ToListAsync();
        Assert.All(allTokensInFamily, t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task RevokeAllForUserAsync_RevokesEveryActiveTokenAcrossAllFamilies()
    {
        using var db = TestDbContextFactory.Create();
        var service = CreateService(db);
        var user = NewUser();
        db.Add(user);
        await db.SaveChangesAsync();

        await service.IssueAsync(user, null, null, default);
        await service.IssueAsync(user, null, null, default);

        await service.RevokeAllForUserAsync(user.Id, default);

        var tokens = await db.Set<RefreshToken>().Where(t => t.UserId == user.Id).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));
    }
}
