namespace NeighborGoods.Api.Features.Account.Contracts.Requests;

public sealed class UpdateProfileRequest
{
    public string? DisplayName { get; init; }

    public string? LineContactId { get; init; }
}
