namespace NeighborGoods.Api.Features.Integrations.Ado;

public sealed class AdoWebhookOptions
{
    public const string SectionName = "AdoWebhook";

    public int MaxEvents { get; set; } = 200;
}
