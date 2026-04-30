using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace NeighborGoods.Api.Features.Messaging;

public sealed class SignalRUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("sub")?.Value
            ?? connection.User?.Identity?.Name;
    }
}
