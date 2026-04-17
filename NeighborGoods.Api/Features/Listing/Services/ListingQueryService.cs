using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Listing.Contracts;
using NeighborGoods.Api.Shared.Contracts;
using NeighborGoods.Api.Shared.Persistence;

namespace NeighborGoods.Api.Features.Listing.Services;

public sealed class ListingQueryService(NeighborGoodsDbContext dbContext)
{
    public async Task<ListingDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var categories = ListingLookupCatalog.Categories.ToDictionary(x => x.Code, x => x.DisplayName);
        var conditions = ListingLookupCatalog.Conditions.ToDictionary(x => x.Code, x => x.DisplayName);
        var residences = ListingLookupCatalog.Residences.ToDictionary(x => x.Code, x => x.DisplayName);

        var listing = await dbContext.Listings
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (listing is null)
        {
            return null;
        }

        return new ListingDetailDto(
            listing.Id,
            listing.Title,
            listing.Description,
            listing.Category,
            categories.GetValueOrDefault(listing.Category, "其他"),
            listing.Condition,
            conditions.GetValueOrDefault(listing.Condition, "良好"),
            Convert.ToInt32(listing.Price),
            listing.Residence,
            residences.GetValueOrDefault(listing.Residence, "未指定"),
            listing.Status,
            listing.CreatedAt,
            listing.UpdatedAt);
    }

    public async Task<PagedResult<ListingListItemDto>> QueryAsync(ListingQueryRequest request, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var categories = ListingLookupCatalog.Categories.ToDictionary(x => x.Code, x => x.DisplayName);
        var conditions = ListingLookupCatalog.Conditions.ToDictionary(x => x.Code, x => x.DisplayName);
        var residences = ListingLookupCatalog.Residences.ToDictionary(x => x.Code, x => x.DisplayName);

        var active = (int)ListingStatus.Active;
        var reserved = (int)ListingStatus.Reserved;

        var queryable = dbContext.Listings
            .Where(x => x.Status == active || x.Status == reserved);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var keyword = request.Query.Trim();
            queryable = queryable.Where(x =>
                EF.Functions.Like(x.Title, $"%{keyword}%") ||
                (x.Description != null && EF.Functions.Like(x.Description, $"%{keyword}%")));
        }

        var total = await queryable.CountAsync(cancellationToken);
        var listings = await queryable
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new ListingSummary(
                x.Id,
                x.Title,
                x.Category,
                x.Condition,
                Convert.ToInt32(x.Price),
                x.Residence))
            .ToListAsync(cancellationToken);

        var items = listings
            .Select(x => new ListingListItemDto(
                x.Id,
                x.Title,
                x.Category,
                categories.GetValueOrDefault(x.Category, "其他"),
                x.Condition,
                conditions.GetValueOrDefault(x.Condition, "良好"),
                x.Price,
                x.Residence,
                residences.GetValueOrDefault(x.Residence, "未指定")
            ))
            .ToList();

        return new PagedResult<ListingListItemDto>(page, pageSize, total, items);
    }
}
