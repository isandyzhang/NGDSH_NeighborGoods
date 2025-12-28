using NeighborGoods.Web.Models.DTOs;
using NeighborGoods.Web.Models.Entities;
using NeighborGoods.Web.Models.ViewModels;

namespace NeighborGoods.Web.Services;

/// <summary>
/// 訊息服務介面
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// 取得未讀訊息數量
    /// </summary>
    Task<int> GetUnreadMessageCountAsync(string userId);

    /// <summary>
    /// 取得對話列表
    /// </summary>
    Task<ConversationListViewModel> GetConversationsAsync(string userId);

    /// <summary>
    /// 取得對話詳情
    /// </summary>
    Task<ChatViewModel?> GetChatAsync(Guid conversationId, string userId);

    /// <summary>
    /// 取得或建立對話
    /// </summary>
    Task<(Conversation conversation, bool isNew)> GetOrCreateConversationAsync(
        string userId1,
        string userId2,
        Guid listingId);

    /// <summary>
    /// 發送訊息
    /// </summary>
    Task<ServiceResult<Guid>> SendMessageAsync(
        Guid? conversationId,
        string? receiverId,
        Guid? listingId,
        string content,
        string senderId);

    /// <summary>
    /// 標記對話為已讀
    /// </summary>
    Task<ServiceResult> MarkAsReadAsync(Guid conversationId, string userId);

    /// <summary>
    /// 驗證商品是否存在
    /// </summary>
    Task<ServiceResult<Models.Entities.Listing>> ValidateListingAsync(Guid listingId);

    /// <summary>
    /// 取得對話資訊（用於 SignalR）
    /// </summary>
    Task<ServiceResult<ConversationInfo>> GetConversationForSignalRAsync(
        Guid? conversationId,
        string senderId,
        string? receiverId,
        Guid? listingId);

    /// <summary>
    /// 發送購買/索取請求
    /// </summary>
    Task<ServiceResult<Guid>> SendPurchaseRequestAsync(
        Guid conversationId,
        Guid listingId,
        string buyerId,
        bool isFreeOrCharity);

    /// <summary>
    /// 賣家同意交易
    /// </summary>
    Task<ServiceResult<AcceptPurchaseResult>> AcceptPurchaseAsync(
        Guid conversationId,
        Guid listingId,
        string sellerId);

    /// <summary>
    /// 買家完成交易
    /// </summary>
    Task<ServiceResult<CompleteTransactionResult>> CompleteTransactionAsync(
        Guid conversationId,
        Guid listingId,
        string buyerId);

    /// <summary>
    /// 取得評價頁面所需資訊
    /// </summary>
    Task<ServiceResult<Models.ViewModels.ReviewViewModel>> GetReviewInfoAsync(
        Guid listingId,
        Guid conversationId,
        string currentUserId);
}

/// <summary>
/// 對話資訊（用於 SignalR）
/// </summary>
public class ConversationInfo
{
    public Guid ConversationId { get; set; }
    public string ReceiverId { get; set; } = string.Empty;
}

/// <summary>
/// 同意交易結果
/// </summary>
public class AcceptPurchaseResult
{
    public Guid MessageId { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
    public DateTime MessageCreatedAt { get; set; }
}

/// <summary>
/// 完成交易結果
/// </summary>
public class CompleteTransactionResult
{
    public Guid MessageId { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
    public DateTime MessageCreatedAt { get; set; }
}

