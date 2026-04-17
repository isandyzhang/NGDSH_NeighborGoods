namespace NeighborGoods.Api.Shared.Security;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string GetRequiredUserId();
}
