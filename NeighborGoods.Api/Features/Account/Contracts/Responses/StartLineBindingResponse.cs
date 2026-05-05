namespace NeighborGoods.Api.Features.Account.Contracts.Responses;

public sealed record StartLineBindingResponse(
    Guid PendingBindingId,
    string LiffUrl,
    string BindingToken,
    string BotLink);
