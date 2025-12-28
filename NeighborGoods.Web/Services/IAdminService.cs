using NeighborGoods.Web.Models.ViewModels;

namespace NeighborGoods.Web.Services;

public interface IAdminService
{
    /// <summary>
    /// 驗證管理員密碼
    /// </summary>
    Task<bool> VerifyPasswordAsync(string password);

    /// <summary>
    /// 取得所有商品列表（分頁）
    /// </summary>
    Task<AdminListingsViewModel> GetAllListingsAsync(int page, int pageSize);

    /// <summary>
    /// 取得商品詳情及相關對話
    /// </summary>
    Task<AdminListingDetailsViewModel?> GetListingDetailsWithConversationsAsync(Guid listingId);

    /// <summary>
    /// 取得所有用戶列表（分頁）
    /// </summary>
    Task<AdminUsersViewModel> GetAllUsersAsync(int page, int pageSize);

    /// <summary>
    /// 刪除商品（管理員權限）
    /// </summary>
    Task<bool> DeleteListingAsync(Guid listingId);

    /// <summary>
    /// 刪除用戶（管理員權限）
    /// </summary>
    Task<bool> DeleteUserAsync(string userId);

    /// <summary>
    /// 發送訊息給管理者
    /// </summary>
    Task<bool> SendMessageToAdminAsync(string senderId, string content);

    /// <summary>
    /// 取得管理員訊息列表（分頁）
    /// </summary>
    Task<AdminMailboxViewModel> GetAdminMessagesAsync(int page, int pageSize, bool? isRead = null);

    /// <summary>
    /// 標記訊息為已讀
    /// </summary>
    Task<bool> MarkMessageAsReadAsync(Guid messageId);

    /// <summary>
    /// 取得未讀訊息數量
    /// </summary>
    Task<int> GetUnreadMessageCountAsync();
}

