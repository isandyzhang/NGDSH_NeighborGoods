using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NeighborGoods.Api.Features.Messaging;

[Authorize]
public sealed class MessageHub : Hub;
