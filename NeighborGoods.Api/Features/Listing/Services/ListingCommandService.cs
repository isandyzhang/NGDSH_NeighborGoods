using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Listing.Contracts;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Security;

namespace NeighborGoods.Api.Features.Listing.Services;

public sealed class ListingCommandService(
    NeighborGoodsDbContext dbContext,
    ICurrentUserContext currentUserContext)
{
    public async Task<Guid> CreateAsync(CreateListingRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Title, request.Price);
        var sellerId = currentUserContext.GetRequiredUserId();
        var seller = await dbContext.AspNetUsers
            .FirstOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            throw new ListingAccessException("AUTH_USER_NOT_FOUND", "找不到登入使用者", StatusCodes.Status401Unauthorized);
        }

        if (!seller.EmailConfirmed)
        {
            throw new ListingAccessException("EMAIL_NOT_CONFIRMED", "請先完成 Email 驗證後再上架", StatusCodes.Status403Forbidden);
        }

        var entity = new Listing
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Price = request.Price,
            IsFree = false,
            IsCharity = false,
            SellerId = sellerId,
            Category = request.CategoryCode,
            PickupLocation = 3,
            Condition = request.ConditionCode,
            BuyerId = null,
            Residence = request.ResidenceCode,
            IsTradeable = false,
            IsPinned = false,
            Status = (int)ListingStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.Listings.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateListingRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Title, request.Price);

        var entity = await dbContext.Listings
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.Title = request.Title.Trim();
        entity.Description = request.Description?.Trim() ?? string.Empty;
        entity.Category = request.CategoryCode;
        entity.Condition = request.ConditionCode;
        entity.Price = request.Price;
        entity.Residence = request.ResidenceCode;
        entity.UpdatedAt = DateTime.UtcNow;

        dbContext.Listings.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Listings
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        dbContext.Listings.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidateRequest(string title, int price)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (price < 0)
        {
            throw new ArgumentException("Price cannot be negative.", nameof(price));
        }
    }

}
