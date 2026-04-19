namespace NeighborGoods.Api.Features.Account.Contracts.Requests;

public sealed class UpdateProfileRequest
{
    public string DisplayName { get; init; } = string.Empty;
}
