namespace NeighborGoods.Api.Features.Admin.Contracts.Requests;

public sealed class AdminSendConversationMessageRequest
{
    public string Content { get; init; } = string.Empty;
}
