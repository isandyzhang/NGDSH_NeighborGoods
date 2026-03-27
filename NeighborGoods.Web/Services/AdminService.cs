using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NeighborGoods.Web.Data;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.Enums;
using NeighborGoods.Web.Models.ViewModels;
using NeighborGoods.Web.Utils;

namespace NeighborGoods.Web.Services;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        AppDbContext db,
        IConfiguration configuration,
        ILogger<AdminService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<bool> VerifyPasswordAsync(string password)
    {
        var adminPassword = _configuration["Admin:Password"];
        return Task.FromResult(!string.IsNullOrEmpty(adminPassword) && adminPassword == password);
    }

    public async Task<AdminDashboardViewModel> GetDashboardStatsAsync()
    {
        var todayStart = TaiwanTime.Now.Date;
        var tomorrowStart = todayStart.AddDays(1);

        var totalListingsCount = await _db.Listings.CountAsync();
        var activeListingsCount = await _db.Listings.CountAsync(l => l.Status == ListingStatus.Active);
        var soldListingsCount = await _db.Listings.CountAsync(l => l.Status == ListingStatus.Sold);
        var inactiveListingsCount = await _db.Listings.CountAsync(l => l.Status == ListingStatus.Inactive);

        // 今日淨增減：以可取得欄位進行簡單估算
        var totalListingsTodayDelta = await _db.Listings.CountAsync(l => l.CreatedAt >= todayStart && l.CreatedAt < tomorrowStart);

        var activeInToday = await _db.Listings.CountAsync(l =>
            l.Status == ListingStatus.Active &&
            l.UpdatedAt >= todayStart &&
            l.UpdatedAt < tomorrowStart);
        var activeOutToday = await _db.Listings.CountAsync(l =>
            l.Status != ListingStatus.Active &&
            l.UpdatedAt >= todayStart &&
            l.UpdatedAt < tomorrowStart);
        var activeListingsTodayDelta = activeInToday - activeOutToday;

        var soldListingsTodayDelta = await _db.Listings.CountAsync(l =>
            l.Status == ListingStatus.Sold &&
            l.UpdatedAt >= todayStart &&
            l.UpdatedAt < tomorrowStart);

        var inactiveListingsTodayDelta = await _db.Listings.CountAsync(l =>
            l.Status == ListingStatus.Inactive &&
            l.UpdatedAt >= todayStart &&
            l.UpdatedAt < tomorrowStart);

        var totalUsersCount = await _db.Users.CountAsync();
        var totalUsersTodayDelta = await _db.Users.CountAsync(u => u.CreatedAt >= todayStart && u.CreatedAt < tomorrowStart);

        var lineNotificationEnabledUsersCount = await _db.Users.CountAsync(u => !string.IsNullOrEmpty(u.LineMessagingApiUserId));
        var lineNotificationEnabledUsersTodayDelta = await _db.Users.CountAsync(u =>
            u.LineMessagingApiAuthorizedAt.HasValue &&
            u.LineMessagingApiAuthorizedAt.Value >= todayStart &&
            u.LineMessagingApiAuthorizedAt.Value < tomorrowStart);

        var emailNotificationEnabledUsersCount = await _db.Users.CountAsync(u =>
            u.EmailNotificationEnabled && !string.IsNullOrEmpty(u.Email));
        var emailNotificationEnabledUsersTodayDelta = await _db.Users.CountAsync(u =>
            u.EmailNotificationEnabled &&
            !string.IsNullOrEmpty(u.Email) &&
            u.CreatedAt >= todayStart &&
            u.CreatedAt < tomorrowStart);

        var emailBoundUsersCount = await _db.Users.CountAsync(u => !string.IsNullOrEmpty(u.Email));
        var emailBoundUsersTodayDelta = await _db.Users.CountAsync(u =>
            u.CreatedAt >= todayStart &&
            u.CreatedAt < tomorrowStart &&
            !string.IsNullOrEmpty(u.Email));

        var recentTopSubmissions = await _db.ListingTopSubmissions
            .Include(s => s.User)
            .OrderByDescending(s => s.CreatedAt)
            .Take(8)
            .Select(s => new AdminDashboardTopSubmissionItemViewModel
            {
                Id = s.Id,
                UserDisplayName = s.User != null ? s.User.DisplayName : "未知用戶",
                FeedbackTitle = s.FeedbackTitle,
                StatusText = s.Status == TopSubmissionStatus.Pending ? "待審核"
                    : s.Status == TopSubmissionStatus.Approved ? "已核准"
                    : "已駁回",
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        var recentMailboxMessages = await _db.AdminMessages
            .Include(m => m.Sender)
            .OrderByDescending(m => m.CreatedAt)
            .Take(8)
            .Select(m => new AdminDashboardMailboxItemViewModel
            {
                Id = m.Id,
                SenderDisplayName = m.Sender != null ? m.Sender.DisplayName : "未知用戶",
                ContentPreview = m.Content.Length > 40 ? m.Content.Substring(0, 40) + "..." : m.Content,
                IsRead = m.IsRead,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return new AdminDashboardViewModel
        {
            TotalListingsCount = totalListingsCount,
            TotalListingsTodayDelta = totalListingsTodayDelta,
            ActiveListingsCount = activeListingsCount,
            ActiveListingsTodayDelta = activeListingsTodayDelta,
            SoldListingsCount = soldListingsCount,
            SoldListingsTodayDelta = soldListingsTodayDelta,
            InactiveListingsCount = inactiveListingsCount,
            InactiveListingsTodayDelta = inactiveListingsTodayDelta,
            TotalUsersCount = totalUsersCount,
            TotalUsersTodayDelta = totalUsersTodayDelta,
            LineNotificationEnabledUsersCount = lineNotificationEnabledUsersCount,
            LineNotificationEnabledUsersTodayDelta = lineNotificationEnabledUsersTodayDelta,
            EmailNotificationEnabledUsersCount = emailNotificationEnabledUsersCount,
            EmailNotificationEnabledUsersTodayDelta = emailNotificationEnabledUsersTodayDelta,
            EmailBoundUsersCount = emailBoundUsersCount,
            EmailBoundUsersTodayDelta = emailBoundUsersTodayDelta,
            RecentTopSubmissions = recentTopSubmissions,
            RecentMailboxMessages = recentMailboxMessages
        };
    }

    public async Task<AdminListingsViewModel> GetAllListingsAsync(int page, int pageSize, ListingStatus? status = null)
    {
        var query = _db.Listings.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value);
        }

        // 先計算總數（不包含 Include）
        var totalCount = await query.CountAsync();

        // 再執行包含 Include 的查詢
        var listings = await query
            .Include(l => l.Seller)
            .Include(l => l.Buyer)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AdminListingItemViewModel
            {
                Id = l.Id,
                Title = l.Title,
                Status = l.Status,
                SellerId = l.SellerId,
                SellerDisplayName = l.Seller != null ? l.Seller.DisplayName : "未知賣家",
                BuyerId = l.BuyerId,
                BuyerDisplayName = l.Buyer != null ? l.Buyer.DisplayName : null,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();

        return new AdminListingsViewModel
        {
            Listings = listings,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            CurrentStatus = status
        };
    }

    public async Task<AdminListingDetailsViewModel?> GetListingDetailsWithConversationsAsync(Guid listingId)
    {
        var listing = await _db.Listings
            .Include(l => l.Seller)
            .Include(l => l.Buyer)
            .Include(l => l.Images.OrderBy(img => img.SortOrder))
            .FirstOrDefaultAsync(l => l.Id == listingId);

        if (listing == null)
        {
            return null;
        }

        // 取得所有相關對話
        var conversations = await _db.Conversations
            .Include(c => c.Participant1)
            .Include(c => c.Participant2)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .ThenInclude(m => m.Sender)
            .Where(c => c.ListingId == listingId)
            .ToListAsync();

        var conversationViewModels = new List<AdminConversationItemViewModel>();

        foreach (var conv in conversations)
        {
            var messages = conv.Messages.Select(m => new AdminMessageItemViewModel
            {
                SenderId = m.SenderId,
                SenderDisplayName = m.Sender != null ? m.Sender.DisplayName : "未知用戶",
                Content = m.Content,
                CreatedAt = m.CreatedAt,
                IsSeller = m.SenderId == listing.SellerId
            }).ToList();

            conversationViewModels.Add(new AdminConversationItemViewModel
            {
                ConversationId = conv.Id,
                Participant1Id = conv.Participant1Id,
                Participant1DisplayName = conv.Participant1 != null ? conv.Participant1.DisplayName : "未知用戶",
                Participant2Id = conv.Participant2Id,
                Participant2DisplayName = conv.Participant2 != null ? conv.Participant2.DisplayName : "未知用戶",
                Messages = messages
            });
        }

        return new AdminListingDetailsViewModel
        {
            Id = listing.Id,
            Title = listing.Title,
            Description = listing.Description,
            Price = listing.Price,
            IsFree = listing.IsFree,
            IsCharity = listing.IsCharity,
            IsTradeable = listing.IsTradeable,
            Status = listing.Status,
            Category = listing.Category,
            Condition = listing.Condition,
            PickupLocation = listing.PickupLocation,
            SellerId = listing.SellerId,
            SellerDisplayName = listing.Seller != null ? listing.Seller.DisplayName : "未知賣家",
            BuyerId = listing.BuyerId,
            BuyerDisplayName = listing.Buyer != null ? listing.Buyer.DisplayName : null,
            CreatedAt = listing.CreatedAt,
            UpdatedAt = listing.UpdatedAt,
            ImageUrls = listing.Images.Select(img => img.ImageUrl).ToList(),
            Conversations = conversationViewModels
        };
    }

    public async Task<AdminUsersViewModel> GetAllUsersAsync(int page, int pageSize)
    {
        var query = _db.Users.AsQueryable();
        var totalCount = await query.CountAsync();

        // 先取得當前頁的用戶 ID 列表
        var userIds = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => u.Id)
            .ToListAsync();

        if (!userIds.Any())
        {
            return new AdminUsersViewModel
            {
                Users = new List<AdminUserItemViewModel>(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        // 一次性取得所有用戶的 ListingCount
        var listingCounts = await _db.Listings
            .Where(l => userIds.Contains(l.SellerId))
            .GroupBy(l => l.SellerId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        // 一次性取得所有用戶的 ConversationCount
        // 分別查詢 Participant1Id 和 Participant2Id，然後在記憶體中合併
        var participant1Data = await _db.Conversations
            .Where(c => userIds.Contains(c.Participant1Id))
            .Select(c => new { UserId = c.Participant1Id, ConversationId = c.Id })
            .ToListAsync();

        var participant2Data = await _db.Conversations
            .Where(c => userIds.Contains(c.Participant2Id))
            .Select(c => new { UserId = c.Participant2Id, ConversationId = c.Id })
            .ToListAsync();

        // 在記憶體中合併並計算總數
        var conversationCounts = participant1Data
            .Concat(participant2Data)
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Select(x => x.ConversationId).Distinct().Count() })
            .ToDictionary(x => x.UserId, x => x.Count);

        // 取得用戶詳細資料
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        // 按照原始順序排序
        var userDict = users.ToDictionary(u => u.Id);
        var userViewModels = userIds
            .Select(id => userDict[id])
            .Select(u => new AdminUserItemViewModel
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Email = u.Email ?? string.Empty,
                IsLineBound = !string.IsNullOrEmpty(u.LineUserId),
                IsNotificationEnabled = !string.IsNullOrEmpty(u.LineMessagingApiUserId),
                CreatedAt = u.CreatedAt,
                ListingCount = listingCounts.GetValueOrDefault(u.Id, 0),
                ConversationCount = conversationCounts.GetValueOrDefault(u.Id, 0)
            })
            .ToList();

        return new AdminUsersViewModel
        {
            Users = userViewModels,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<bool> DeleteListingAsync(Guid listingId)
    {
        try
        {
            var listing = await _db.Listings
                .Include(l => l.Images)
                .FirstOrDefaultAsync(l => l.Id == listingId);

            if (listing == null)
            {
                return false;
            }

            // 1. 先刪除相關的 Conversation 和 Message
            var conversations = await _db.Conversations
                .Where(c => c.ListingId == listingId)
                .Include(c => c.Messages)
                .ToListAsync();

            foreach (var conversation in conversations)
            {
                // Message 會因為 Cascade 自動刪除，但明確刪除更安全
                _db.Messages.RemoveRange(conversation.Messages);
            }
            _db.Conversations.RemoveRange(conversations);

            // 2. 刪除相關的 Review（雖然是 Cascade，但明確刪除更安全）
            var reviews = await _db.Reviews
                .Where(r => r.ListingId == listingId)
                .ToListAsync();
            _db.Reviews.RemoveRange(reviews);

            // 3. 刪除相關圖片（雖然是 Cascade，但明確刪除更安全）
            _db.ListingImages.RemoveRange(listing.Images);

            // 4. 最後刪除 Listing
            _db.Listings.Remove(listing);
            
            await _db.SaveChangesAsync();

            _logger.LogInformation("管理員刪除商品：ListingId={ListingId}", listingId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除商品時發生錯誤：ListingId={ListingId}", listingId);
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        try
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return false;
            }

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // 1. 刪除用戶的 Listings 及其相關資料
                var listings = await _db.Listings
                    .Where(l => l.SellerId == userId)
                    .Include(l => l.Images)
                    .ToListAsync();

                foreach (var listing in listings)
                {
                    // 刪除相關的 Conversation 和 Message
                    var conversations = await _db.Conversations
                        .Where(c => c.ListingId == listing.Id)
                        .Include(c => c.Messages)
                        .ToListAsync();

                    foreach (var conv in conversations)
                    {
                        _db.Messages.RemoveRange(conv.Messages);
                    }
                    _db.Conversations.RemoveRange(conversations);

                    // 刪除相關的 Review
                    var listingReviews = await _db.Reviews
                        .Where(r => r.ListingId == listing.Id)
                        .ToListAsync();
                    _db.Reviews.RemoveRange(listingReviews);

                    // 刪除相關圖片
                    _db.ListingImages.RemoveRange(listing.Images);

                    // 刪除 Listing
                    _db.Listings.Remove(listing);
                }

                // 2. 刪除用戶參與的 Conversations（作為參與者）
                var userConversations = await _db.Conversations
                    .Where(c => c.Participant1Id == userId || c.Participant2Id == userId)
                    .Include(c => c.Messages)
                    .ToListAsync();

                foreach (var conv in userConversations)
                {
                    _db.Messages.RemoveRange(conv.Messages);
                }
                _db.Conversations.RemoveRange(userConversations);

                // 3. 刪除用戶的 Reviews（作為賣家或買家）
                var userReviews = await _db.Reviews
                    .Where(r => r.SellerId == userId || r.BuyerId == userId)
                    .ToListAsync();
                _db.Reviews.RemoveRange(userReviews);

                // 4. 刪除用戶的 AdminMessages
                var adminMessages = await _db.AdminMessages
                    .Where(m => m.SenderId == userId)
                    .ToListAsync();
                _db.AdminMessages.RemoveRange(adminMessages);

                // 5. 刪除用戶的 LineBindingPending
                var lineBindings = await _db.LineBindingPending
                    .Where(l => l.UserId == userId)
                    .ToListAsync();
                _db.LineBindingPending.RemoveRange(lineBindings);

                // 6. 最後刪除用戶
                _db.Users.Remove(user);
                
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("管理員刪除用戶：UserId={UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "刪除用戶 {UserId} 時發生錯誤，已回滾", userId);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除用戶時發生錯誤：UserId={UserId}", userId);
            return false;
        }
    }

    public async Task<bool> SendMessageToAdminAsync(string senderId, string content)
    {
        try
        {
            var message = new AdminMessage
            {
                Id = Guid.NewGuid(),
                SenderId = senderId,
                Content = content.Trim(),
                IsRead = false,
                CreatedAt = TaiwanTime.Now
            };

            _db.AdminMessages.Add(message);
            await _db.SaveChangesAsync();

            _logger.LogInformation("用戶發送訊息給管理員：SenderId={SenderId}, MessageId={MessageId}", senderId, message.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "發送訊息給管理員時發生錯誤：SenderId={SenderId}", senderId);
            return false;
        }
    }

    public async Task<AdminMailboxViewModel> GetAdminMessagesAsync(int page, int pageSize, bool? isRead = null)
    {
        var query = _db.AdminMessages
            .Include(m => m.Sender)
            .AsQueryable();

        if (isRead.HasValue)
        {
            query = query.Where(m => m.IsRead == isRead.Value);
        }

        var totalCount = await query.CountAsync();
        var unreadCount = await _db.AdminMessages.CountAsync(m => !m.IsRead);

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new AdminMailboxItemViewModel
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderDisplayName = m.Sender != null ? m.Sender.DisplayName : "未知用戶",
                Content = m.Content,
                IsRead = m.IsRead,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return new AdminMailboxViewModel
        {
            Messages = messages,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            UnreadCount = unreadCount
        };
    }

    public async Task<bool> MarkMessageAsReadAsync(Guid messageId)
    {
        try
        {
            var message = await _db.AdminMessages
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null)
            {
                return false;
            }

            message.IsRead = true;
            await _db.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "標記訊息為已讀時發生錯誤：MessageId={MessageId}", messageId);
            return false;
        }
    }

    public async Task<int> GetUnreadMessageCountAsync()
    {
        return await _db.AdminMessages.CountAsync(m => !m.IsRead);
    }
}

