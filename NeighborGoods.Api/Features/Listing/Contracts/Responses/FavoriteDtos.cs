namespace NeighborGoods.Api.Features.Listing.Contracts;

public sealed record FavoriteToggleDto(
    Guid ListingId,
    bool IsFavorited,
    int FavoriteCount,
    DateTime? FavoritedAt
);

public sealed record FavoriteStatusDto(
    Guid ListingId,
    int FavoriteCount,
    bool IsFavorited
);

public sealed record MyFavoriteListItemDto(
    Guid ListingId,
    string Title,
    int CategoryCode,
    string CategoryName,
    int Price,
    bool IsFree,
    string? MainImageUrl,
    DateTime FavoritedAt
);

public sealed record InterestCategoryDto(
    int CategoryCode,
    string CategoryName,
    double Score,
    int FavoriteCount
);

public sealed record InterestProfileDto(
    string UserId,
    int WindowDays,
    IReadOnlyList<InterestCategoryDto> TopCategories,
    DateTime UpdatedAt
);

public sealed record PushTargetDto(
    string UserId,
    string? LineUserId,
    string? Email,
    double Score
);

public sealed record PushTargetsResultDto(
    Guid? ListingId,
    int CategoryCode,
    string CategoryName,
    IReadOnlyList<PushTargetDto> Targets
);
