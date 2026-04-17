using System;
using System.Collections.Generic;

namespace NeighborGoods.Api.Shared.Persistence.LegacyEntities;

public partial class AdminMessage
{
    public Guid Id { get; set; }

    public string SenderId { get; set; } = null!;

    public string Content { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AspNetUser Sender { get; set; } = null!;
}
