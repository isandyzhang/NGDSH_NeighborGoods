using Microsoft.AspNetCore.SignalR;

namespace NeighborGoods.Web.Hubs;

public class MessageHub : Hub
{
    /// <summary>
    /// 發送訊息給特定用戶
    /// </summary>
    public async Task SendMessage(string receiverUserId, string senderDisplayName, string content, DateTime createdAt)
    {
        // 推送到特定用戶
        await Clients.User(receiverUserId).SendAsync("ReceiveMessage", senderDisplayName, content, createdAt);
    }

    /// <summary>
    /// 當用戶連線時，將用戶加入對應的群組
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            // 將用戶加入以自己 UserId 命名的群組，方便推送訊息
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        }
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 當用戶斷線時，從群組中移除
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}

