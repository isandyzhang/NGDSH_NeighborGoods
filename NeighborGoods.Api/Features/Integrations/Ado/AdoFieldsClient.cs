using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NeighborGoods.Api.Features.Integrations.Ado;

public sealed class AdoFieldsClient(
    HttpClient httpClient,
    IOptions<AdoWebhookOptions> options,
    ILogger<AdoFieldsClient> logger)
{
    private readonly AdoWebhookOptions _options = options.Value;

    public async Task<IReadOnlyDictionary<string, string>> GetFieldDisplayNamesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.PersonalAccessToken)
            || string.IsNullOrWhiteSpace(_options.OrganizationUrl))
        {
            return new Dictionary<string, string>();
        }

        var baseUrl = _options.OrganizationUrl.TrimEnd('/');
        var requestUri = $"{baseUrl}/_apis/wit/fields?api-version={Uri.EscapeDataString(_options.ApiVersion)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_options.PersonalAccessToken}")));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "ADO fields API failed: status={StatusCode} body={Body}",
                response.StatusCode,
                body);
            throw new InvalidOperationException($"ADO fields API failed with status {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("value", out var valueElement)
            || valueElement.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, string>();
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in valueElement.EnumerateArray())
        {
            if (!field.TryGetProperty("referenceName", out var referenceNameElement))
            {
                continue;
            }

            var referenceName = referenceNameElement.GetString();
            if (string.IsNullOrWhiteSpace(referenceName))
            {
                continue;
            }

            var displayName = field.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;
            map[referenceName] = string.IsNullOrWhiteSpace(displayName) ? referenceName : displayName;
        }

        return map;
    }
}
