namespace NeighborGoods.Api.Features.Surveys.Contracts;

public sealed record SubmitSurveyRequest(
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
    bool ConsentAccepted);
