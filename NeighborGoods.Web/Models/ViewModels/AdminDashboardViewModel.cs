namespace NeighborGoods.Web.Models.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalListingsCount { get; set; }
    public int TotalListingsTodayDelta { get; set; }

    public int ActiveListingsCount { get; set; }
    public int ActiveListingsTodayDelta { get; set; }

    public int SoldListingsCount { get; set; }
    public int SoldListingsTodayDelta { get; set; }

    public int InactiveListingsCount { get; set; }
    public int InactiveListingsTodayDelta { get; set; }

    public int TotalUsersCount { get; set; }
    public int TotalUsersTodayDelta { get; set; }

    public int LineNotificationEnabledUsersCount { get; set; }
    public int LineNotificationEnabledUsersTodayDelta { get; set; }

    public int EmailNotificationEnabledUsersCount { get; set; }
    public int EmailNotificationEnabledUsersTodayDelta { get; set; }

    public int EmailBoundUsersCount { get; set; }
    public int EmailBoundUsersTodayDelta { get; set; }

    public List<AdminDashboardTopSubmissionItemViewModel> RecentTopSubmissions { get; set; } = new();
    public List<AdminDashboardMailboxItemViewModel> RecentMailboxMessages { get; set; } = new();
}

public class AdminDashboardTopSubmissionItemViewModel
{
    public int Id { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public string FeedbackTitle { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AdminDashboardMailboxItemViewModel
{
    public Guid Id { get; set; }
    public string SenderDisplayName { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
