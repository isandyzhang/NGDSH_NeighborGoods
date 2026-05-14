namespace NeighborGoods.Web.Models.ViewModels;

public class AdminUsersViewModel
{
    public List<AdminUserItemViewModel> Users { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class AdminUserItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsLineBound { get; set; }
    public bool IsNotificationEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ListingCount { get; set; }
    public int ConversationCount { get; set; }
}

