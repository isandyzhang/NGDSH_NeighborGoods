namespace NeighborGoods.Api.Features.Account.Contracts.Responses;

public sealed record StartLineBindingResponse(
    Guid PendingBindingId,
    string BotLink,
    string QrCodeUrl
);
