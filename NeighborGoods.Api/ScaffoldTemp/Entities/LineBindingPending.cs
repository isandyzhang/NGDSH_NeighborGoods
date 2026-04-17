using System;
using System.Collections.Generic;

namespace NeighborGoods.Api.Shared.Persistence.LegacyEntities;

public partial class LineBindingPending
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = null!;

    public string Token { get; set; } = null!;

    public string? LineUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AspNetUser User { get; set; } = null!;
}
