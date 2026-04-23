using Microsoft.EntityFrameworkCore;
using NeighborGoods.Api.Features.Listing;
using NeighborGoods.Api.Features.PurchaseRequests;
using NeighborGoods.Api.Features.Reviews.Contracts;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Features.Reviews.Services;

public sealed class ReviewService(NeighborGoodsDbContext dbContext)
{
    public async Task<(PurchaseRequestReviewStatusDto? Data, string? ErrorCode, string? ErrorMessage)> GetStatusAsync(
        string currentUserId,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await dbContext.PurchaseRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == purchaseRequestId, cancellationToken);
        if (request is null)
        {
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "找不到交易請求");
        }

        if (!string.Equals(request.BuyerId, currentUserId, StringComparison.Ordinal) &&
            !string.Equals(request.SellerId, currentUserId, StringComparison.Ordinal))
        {
            return (null, "PURCHASE_REQUEST_ACCESS_DENIED", "無權限查看此交易請求的評價狀態");
        }

        var review = await dbContext.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ListingId == request.ListingId && x.BuyerId == request.BuyerId,
                cancellationToken);
        var reviewDetail = review is null
            ? null
            : new ReviewDetailDto(
                review.Id,
                review.ListingId,
                review.SellerId,
                review.BuyerId,
                review.Rating,
                review.Content,
                review.CreatedAt);

        var (canReview, reason) = await EvaluateCanReviewAsync(request, currentUserId, cancellationToken);
        return (new PurchaseRequestReviewStatusDto(
            request.Id,
            canReview,
            review is not null,
            reason,
            reviewDetail), null, null);
    }

    public async Task<(ReviewDetailDto? Data, string? ErrorCode, string? ErrorMessage)> CreateAsync(
        string currentUserId,
        Guid purchaseRequestId,
        CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Rating is < 1 or > 5)
        {
            return (null, "VALIDATION_ERROR", "評分需介於 1 到 5");
        }

        var purchaseRequest = await dbContext.PurchaseRequests
            .FirstOrDefaultAsync(x => x.Id == purchaseRequestId, cancellationToken);
        if (purchaseRequest is null)
        {
            return (null, "PURCHASE_REQUEST_NOT_FOUND", "找不到交易請求");
        }

        if (!string.Equals(purchaseRequest.BuyerId, currentUserId, StringComparison.Ordinal))
        {
            return (null, "PURCHASE_REQUEST_ACCESS_DENIED", "僅買家本人可提交評價");
        }

        var existing = await dbContext.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ListingId == purchaseRequest.ListingId && x.BuyerId == purchaseRequest.BuyerId,
                cancellationToken);
        if (existing is not null)
        {
            return (null, "REVIEW_ALREADY_EXISTS", "此筆交易已提交過評價");
        }

        var (canReview, reason) = await EvaluateCanReviewAsync(purchaseRequest, currentUserId, cancellationToken);
        if (!canReview)
        {
            return (null, "REVIEW_NOT_AVAILABLE", reason ?? "目前尚不可評價");
        }

        var entity = new Review
        {
            Id = Guid.NewGuid(),
            ListingId = purchaseRequest.ListingId,
            SellerId = purchaseRequest.SellerId,
            BuyerId = purchaseRequest.BuyerId,
            Rating = request.Rating,
            Content = string.IsNullOrWhiteSpace(request.Content) ? null : request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Reviews.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (new ReviewDetailDto(
            entity.Id,
            entity.ListingId,
            entity.SellerId,
            entity.BuyerId,
            entity.Rating,
            entity.Content,
            entity.CreatedAt), null, null);
    }

    private async Task<(bool CanReview, string? Reason)> EvaluateCanReviewAsync(
        PurchaseRequest request,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.BuyerId, currentUserId, StringComparison.Ordinal))
        {
            return (false, "僅買家可填寫評價");
        }

        if ((PurchaseRequestStatus)request.Status != PurchaseRequestStatus.Accepted)
        {
            return (false, "僅已接受的交易請求可評價");
        }

        var listingStatus = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Id == request.ListingId)
            .Select(x => (ListingStatus?)x.Status)
            .FirstOrDefaultAsync(cancellationToken);
        if (listingStatus is null)
        {
            return (false, "找不到對應商品");
        }

        if (listingStatus is not (ListingStatus.Sold or ListingStatus.Donated or ListingStatus.GivenOrTraded))
        {
            return (false, "商品需完成交易（已售出/已贈與/已易物）後才能評價");
        }

        return (true, null);
    }
}
