namespace NeighborGoods.Data.Surveys;

public sealed class SurveyResponse
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>填寫者帳號（每人一筆／限領一瓶）。</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>social | non_social</summary>
    public string ResidentType { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? Phone { get; set; }

    /// <summary>south | north（僅社宅）</summary>
    public string? Building { get; set; }

    public int? Floor { get; set; }

    /// <summary>satisfied | fair | unsatisfied</summary>
    public string RatingUi { get; set; } = string.Empty;

    public string RatingListing { get; set; } = string.Empty;

    public string RatingInteraction { get; set; } = string.Empty;

    public string RatingGuide { get; set; } = string.Empty;

    public string RatingPromo { get; set; } = string.Empty;

    /// <summary>friends | bulletin | youth_online | other</summary>
    public string Channel { get; set; } = string.Empty;

    public string? Feedback { get; set; }

    public bool ConsentAccepted { get; set; }

    public string ActivityName { get; set; } = string.Empty;

    /// <summary>前 24 名贈品資格（御茶園麥茶）。</summary>
    public bool IsGiftEligible { get; set; }
}
