namespace NeighborGoods.Api.Features.Integrations.Ado;

public sealed class AdoWebhookOptions
{
    public const string SectionName = "AdoWebhook";

    public int MaxEvents { get; set; } = 200;

    public string OrganizationUrl { get; set; } = string.Empty;

    public string PersonalAccessToken { get; set; } = string.Empty;

    public string ApiVersion { get; set; } = "7.1";

    public bool LineNotifyEnabled { get; set; } = true;

    /// <summary>ADO 通知目標 LINE 群組 ID（C 開頭）。bot 入群後從 log 取得再填入。</summary>
    public string LineGroupId { get; set; } = "C65c77c17d0c22de6a5b7d820aa35c4e9";
}
