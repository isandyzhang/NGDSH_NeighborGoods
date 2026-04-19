namespace NeighborGoods.Api.Features.Account.Contracts.Requests;

public sealed class ConfirmLineBindingRequest
{
    public Guid PendingBindingId { get; init; }
}
