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

        // 同一筆成交（同商品、同買賣家）只應有一份雙向評價；遷移或歷史資料可能把 PurchaseRequestId 指到非本次檢視的 PR，仍須納入同一交易配對下的既有評價。
        var reviews = await dbContext.Reviews
            .AsNoTracking()
            .Where(x =>
                x.ListingId == request.ListingId &&
                x.BuyerId == request.BuyerId &&
                x.SellerId == request.SellerId)
            .ToListAsync(cancellationToken);

        Review? buyerReviewEntity = reviews.FirstOrDefault(r =>
            string.Equals(r.ReviewerId, request.BuyerId, StringComparison.Ordinal))
            ?? reviews.FirstOrDefault(r =>
                r.ReviewerId is null && string.Equals(r.BuyerId, request.BuyerId, StringComparison.Ordinal));
        Review? sellerReviewEntity = reviews.FirstOrDefault(r =>
            string.Equals(r.ReviewerId, request.SellerId, StringComparison.Ordinal));

        var (buyerCan, buyerReason) = await EvaluateCanReviewAsync(request, request.BuyerId, cancellationToken);
        var (sellerCan, sellerReason) = await EvaluateCanReviewAsync(request, request.SellerId, cancellationToken);

        var buyerCanReview = buyerCan && buyerReviewEntity is null;
        var sellerCanReview = sellerCan && sellerReviewEntity is null;
        var viewerIsBuyer = string.Equals(request.BuyerId, currentUserId, StringComparison.Ordinal);
        var viewerCanReview = viewerIsBuyer ? buyerCanReview : sellerCanReview;
        var viewerReviewed = viewerIsBuyer ? buyerReviewEntity is not null : sellerReviewEntity is not null;
        var viewerReviewBlockReason = viewerIsBuyer
            ? (buyerReviewEntity is not null ? null : buyerReason)
            : (sellerReviewEntity is not null ? null : sellerReason);
        var viewerReview = viewerIsBuyer
            ? (buyerReviewEntity is null ? null : ToDetailDto(buyerReviewEntity, purchaseRequestId))
            : (sellerReviewEntity is null ? null : ToDetailDto(sellerReviewEntity, purchaseRequestId));

        return (new PurchaseRequestReviewStatusDto(
            request.Id,
            buyerCanReview,
            buyerReviewEntity is not null,
            buyerReviewEntity is not null ? null : buyerReason,
            buyerReviewEntity is null ? null : ToDetailDto(buyerReviewEntity, purchaseRequestId),
            sellerCanReview,
            sellerReviewEntity is not null,
            sellerReviewEntity is not null ? null : sellerReason,
            sellerReviewEntity is null ? null : ToDetailDto(sellerReviewEntity, purchaseRequestId),
            viewerIsBuyer,
            viewerCanReview,
            viewerReviewed,
            viewerReviewBlockReason,
            viewerReview), null, null);
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

        if (!string.Equals(purchaseRequest.BuyerId, currentUserId, StringComparison.Ordinal) &&
            !string.Equals(purchaseRequest.SellerId, currentUserId, StringComparison.Ordinal))
        {
            return (null, "PURCHASE_REQUEST_ACCESS_DENIED", "僅買家或賣家本人可提交評價");
        }

        var existingSameDeal = await dbContext.Reviews
            .AsNoTracking()
            .AnyAsync(
                x => x.ListingId == purchaseRequest.ListingId
                    && x.BuyerId == purchaseRequest.BuyerId
                    && x.SellerId == purchaseRequest.SellerId
                    && (
                        x.ReviewerId == currentUserId
                        || (x.ReviewerId == null && string.Equals(purchaseRequest.BuyerId, currentUserId, StringComparison.Ordinal))),
                cancellationToken);
        if (existingSameDeal)
        {
            return (null, "REVIEW_ALREADY_EXISTS", "你已提交過此筆交易的評價");
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
            PurchaseRequestId = purchaseRequest.Id,
            ReviewerId = currentUserId,
            Rating = request.Rating,
            Content = string.IsNullOrWhiteSpace(request.Content) ? null : request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Reviews.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (ToDetailDto(entity, purchaseRequest.Id), null, null);
    }

    private static ReviewDetailDto ToDetailDto(Review review, Guid purchaseRequestId) =>
        new(
            review.Id,
            review.PurchaseRequestId ?? purchaseRequestId,
            review.ReviewerId ?? review.BuyerId,
            review.ListingId,
            review.SellerId,
            review.BuyerId,
            review.Rating,
            review.Content,
            review.CreatedAt);

    private async Task<(bool CanReview, string? Reason)> EvaluateCanReviewAsync(
        PurchaseRequest request,
        string reviewerUserId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.BuyerId, reviewerUserId, StringComparison.Ordinal) &&
            !string.Equals(request.SellerId, reviewerUserId, StringComparison.Ordinal))
        {
            return (false, "僅買家或賣家可填寫評價");
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
