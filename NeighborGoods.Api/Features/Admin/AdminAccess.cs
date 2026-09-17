using Microsoft.EntityFrameworkCore;
using NeighborGoods.Data;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Admin;

public static class AdminAccess
{
    public const int AdminRoleCode = 3;

    public static async Task<bool> IsAdminAsync(
        ICurrentUserContext currentUser,
        NeighborGoodsDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.GetRequiredUserId();
        return await dbContext.AspNetUsers
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.Role == AdminRoleCode, cancellationToken);
    }
}
