namespace NeighborGoods.Web.Models.ViewModels;

public class AdminMailboxViewModel
{
    public List<AdminMailboxItemViewModel> Messages { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public int UnreadCount { get; set; }
}

public class AdminMailboxItemViewModel
{
    public Guid Id { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

