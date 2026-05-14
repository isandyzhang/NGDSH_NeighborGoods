namespace NeighborGoods.Web.Models.ViewModels;

public class MessageViewModel
{
    public Guid Id { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsMine { get; set; }
}

