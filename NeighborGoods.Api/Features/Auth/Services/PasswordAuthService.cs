using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Auth.Services;

public sealed class PasswordAuthService(NeighborGoodsDbContext dbContext)
{
    private readonly PasswordHasher<AspNetUser> _passwordHasher = new();

    public async Task<AspNetUser?> ValidateCredentialsAsync(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userNameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalized = userNameOrEmail.Trim().ToUpperInvariant();
        var user = await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x =>
                x.NormalizedUserName == normalized ||
                x.NormalizedEmail == normalized, cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return user;
    }
}
