namespace NeighborGoods.Web.Models.ViewModels;

public class SendPurchaseRequestViewModel
{
    public Guid ConversationId { get; set; }
    public Guid ListingId { get; set; }
    public bool IsFreeOrCharity { get; set; }
}

