namespace NeighborGoods.Api.Features.Integrations.Ado;

public sealed class AdoWebhookOptions
{
    public const string SectionName = "AdoWebhook";

    public int MaxEvents { get; set; } = 200;

    public string OrganizationUrl { get; set; } = string.Empty;

    public string PersonalAccessToken { get; set; } = string.Empty;

    public string ApiVersion { get; set; } = "7.1";
}
