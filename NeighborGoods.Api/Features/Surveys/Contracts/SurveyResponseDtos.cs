namespace NeighborGoods.Api.Features.Surveys.Contracts;

public sealed record SurveySubmitResponse(
    Guid Id,
    DateTime CreatedAt,
    bool IsGiftEligible,
    int RemainingGiftSlots);

public sealed record SurveyGiftStatusResponse(
    string GiftName,
    string PickupNote,
    int GiftLimit,
    int RemainingGiftSlots,
    bool IsAvailable,
    string? GiftListingPath,
    bool AlreadySubmitted,
    bool AlreadyGiftEligible);

public sealed record AdminSurveyListItemDto(
    Guid Id,
    DateTime CreatedAt,
    string UserId,
    string ResidentType,
    string? DisplayName,
    string? Phone,
    string? Building,
    int? Floor,
    string RatingUi,
    string RatingListing,
    string RatingInteraction,
    string RatingGuide,
    string RatingPromo,
    string Channel,
    string? Feedback,
    string ActivityName,
    bool IsGiftEligible);

public sealed record AdminSurveyListResponse(
    IReadOnlyList<AdminSurveyListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    int RemainingGiftSlots,
    int GiftLimit);
