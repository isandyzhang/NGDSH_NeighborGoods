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
}

