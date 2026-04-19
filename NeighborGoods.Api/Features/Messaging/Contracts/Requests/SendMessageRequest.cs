namespace NeighborGoods.Api.Features.Messaging.Contracts.Requests;

public sealed class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
}
