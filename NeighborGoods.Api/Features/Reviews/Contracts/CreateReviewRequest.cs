namespace NeighborGoods.Api.Features.Reviews.Contracts;

public sealed record CreateReviewRequest(
    int Rating,
    string? Content
);
