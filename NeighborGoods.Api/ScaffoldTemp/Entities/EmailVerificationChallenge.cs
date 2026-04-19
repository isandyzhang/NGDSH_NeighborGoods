namespace NeighborGoods.Api.Shared.Persistence.LegacyEntities;

public sealed class EmailVerificationChallenge
{
    public Guid Id { get; set; }

    public byte Purpose { get; set; }

    public string NormalizedEmail { get; set; } = string.Empty;

    public string? UserId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConsumedAt { get; set; }
}
