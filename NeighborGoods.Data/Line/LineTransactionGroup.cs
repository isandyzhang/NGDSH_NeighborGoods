namespace NeighborGoods.Data.Line;

public sealed class LineTransactionGroup
{
    public Guid Id { get; set; }

    public string GroupId { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime JoinedAt { get; set; }

    public DateTime? LeftAt { get; set; }
}
