namespace NeighborGoods.Web.Models.ViewModels;

public class ChatViewModel
{
    public Guid ConversationId { get; set; }
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherUserDisplayName { get; set; } = string.Empty;
    public List<MessageViewModel> Messages { get; set; } = new List<MessageViewModel>();
}

