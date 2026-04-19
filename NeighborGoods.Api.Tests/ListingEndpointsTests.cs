using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeighborGoods.Api.Features.Listing.Services;
using NeighborGoods.Api.Shared.Persistence;
using NeighborGoods.Api.Shared.Persistence.LegacyEntities;

namespace NeighborGoods.Api.Tests;

[Collection("ListingApiTests")]
public sealed class ListingEndpointsTests
{
    private static readonly JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Guid SeededTesterListingId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const string ConfirmedUserId = "test-user-confirmed";
    private const string OtherUserId = "test-user-other";
    private const string UnconfirmedUserId = "test-user-unconfirmed";

    private const string ConfirmedUserName = "tester";
    private const string UnconfirmedUserName = "novalid";
    private const string UserPassword = "Passw0rd!";

    private readonly SqlServerContainerFixture _fixture;

    public ListingEndpointsTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetListings_ReturnsSuccessEnvelope()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/listings?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.True(body.GetProperty("data").GetProperty("items").GetArrayLength() >= 1);
        var firstItem = body.GetProperty("data").GetProperty("items")[0];
        Assert.True(firstItem.TryGetProperty("mainImageUrl", out _));
    }

    [Fact]
    public async Task GetListings_IsCharityTrue_FiltersToCharityListings()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/listings?q=filter-test&isCharity=true&page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").GetProperty("items");
        var titles = GetListingTitles(items);
        Assert.Contains("filter-test-charity-only", titles);
        Assert.Contains("filter-test-both-flags", titles);
        Assert.DoesNotContain("filter-test-free-only", titles);
        Assert.DoesNotContain("filter-test-tradeable-only", titles);
    }

    [Fact]
    public async Task GetListings_IsFreeTrue_FiltersToFreeListings()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/listings?q=filter-test&isFree=true&page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var titles = GetListingTitles(body.GetProperty("data").GetProperty("items"));
        Assert.Contains("filter-test-free-only", titles);
        Assert.Contains("filter-test-both-flags", titles);
        Assert.DoesNotContain("filter-test-charity-only", titles);
    }

    [Fact]
    public async Task GetListings_IsCharityAndIsFreeTrue_AndFilters()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/listings?q=filter-test&isCharity=true&isFree=true&page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var titles = GetListingTitles(body.GetProperty("data").GetProperty("items"));
        Assert.Single(titles);
        Assert.Contains("filter-test-both-flags", titles);
    }

    [Fact]
    public async Task GetListings_IsTradeableTrue_FiltersToTradeableListings()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/listings?q=filter-test&isTradeable=true&page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var titles = GetListingTitles(body.GetProperty("data").GetProperty("items"));
        Assert.Single(titles);
        Assert.Contains("filter-test-tradeable-only", titles);
    }

    [Fact]
    public async Task GetLookupCategories_ReturnsSeededList()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/lookups/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        var items = body.GetProperty("data");
        Assert.Equal(10, items.GetArrayLength());
        Assert.Equal(0, items[0].GetProperty("id").GetInt32());
        Assert.Equal("Furniture", items[0].GetProperty("codeKey").GetString());
        Assert.Equal("家具家飾", items[0].GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task GetLookupConditions_ReturnsSeededList()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/lookups/conditions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Equal(5, body.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task GetLookupResidences_ReturnsSeededList()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/lookups/residences");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Equal(4, body.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task GetLookupPickupLocations_ReturnsSeededList()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/lookups/pickup-locations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Equal(4, body.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task GetListingById_WhenMissing_ReturnsNotFound()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/listings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("LISTING_NOT_FOUND", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetListingById_ReturnsDualCompatibleImageUrls()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/listings/{SeededTesterListingId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());

        var data = body.GetProperty("data");
        Assert.Equal(
            "https://legacy-images.test/listings/11111111-1111-1111-1111-111111111111/0-old.jpg",
            data.GetProperty("mainImageUrl").GetString());

        var imageUrls = data.GetProperty("imageUrls");
        Assert.Equal(2, imageUrls.GetArrayLength());
        Assert.Equal(
            "https://legacy-images.test/listings/11111111-1111-1111-1111-111111111111/0-old.jpg",
            imageUrls[0].GetString());
        Assert.Equal(
            "https://blob.local.test/listing/listings/11111111-1111-1111-1111-111111111111/1-new-path.jpg",
            imageUrls[1].GetString());
    }

    [Fact]
    public async Task PostListing_CreatesResource()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var request = new
        {
            title = "新刊登測試",
            description = "測試描述",
            categoryCode = 1,
            conditionCode = 2,
            price = 1500,
            residenceCode = 2,
            pickupLocationCode = 3
        };

        using var form = BuildCreateListingForm(request);
        var response = await client.PostAsync("/api/v1/listings", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        var id = body.GetProperty("data").GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task PostListing_InvalidCategoryCode_ReturnsBadRequest()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var request = new
        {
            title = "無效分類",
            description = "測試",
            categoryCode = 999,
            conditionCode = 1,
            price = 100,
            residenceCode = 2,
            pickupLocationCode = 3
        };

        using var form = BuildCreateListingForm(request);
        var response = await client.PostAsync("/api/v1/listings", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostListing_InvalidPickupLocationCode_ReturnsBadRequest()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var request = new
        {
            title = "無效面交",
            description = "測試",
            categoryCode = 1,
            conditionCode = 1,
            price = 100,
            residenceCode = 2,
            pickupLocationCode = 99
        };

        using var form = BuildCreateListingForm(request);
        var response = await client.PostAsync("/api/v1/listings", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostListing_WhenUnauthenticated_ReturnsUnauthorized()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var request = new
        {
            title = "匿名上架",
            description = "不應成功",
            categoryCode = 1,
            conditionCode = 2,
            price = 100,
            residenceCode = 2,
            pickupLocationCode = 3
        };

        using var form = BuildCreateListingForm(request);
        var response = await client.PostAsync("/api/v1/listings", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostListing_WhenEmailNotConfirmed_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, UnconfirmedUserName, UserPassword);

        var request = new
        {
            title = "未驗證上架",
            description = "不應成功",
            categoryCode = 1,
            conditionCode = 2,
            price = 100,
            residenceCode = 2,
            pickupLocationCode = 3
        };

        using var form = BuildCreateListingForm(request);
        var response = await client.PostAsync("/api/v1/listings", form);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("EMAIL_NOT_CONFIRMED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostListing_WithoutImages_ReturnsBadRequest()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var payload = new
        {
            title = "缺圖",
            description = "x",
            categoryCode = 1,
            conditionCode = 1,
            price = 100,
            residenceCode = 2,
            pickupLocationCode = 3
        };
        using var form = new MultipartFormDataContent();
        form.Add(
            new StringContent(JsonSerializer.Serialize(payload, CamelCaseJson), Encoding.UTF8, "application/json"),
            "payload");

        var response = await client.PostAsync("/api/v1/listings", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostListing_InvalidPayloadJson_ReturnsBadRequest()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("not-json{{{", Encoding.UTF8, "application/json"), "payload");
        var fileContent = new ByteArrayContent([9, 9, 9]);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        form.Add(fileContent, "images", "a.jpg");

        var response = await client.PostAsync("/api/v1/listings", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostListing_TooManyImages_ReturnsBadRequest()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        using var form = BuildCreateListingForm(new
        {
            title = "超張數",
            description = "x",
            categoryCode = 1,
            conditionCode = 1,
            price = 100,
            residenceCode = 2,
            pickupLocationCode = 3
        }, imageCount: 6);

        var response = await client.PostAsync("/api/v1/listings", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostListing_WhenJsonBody_Returns415()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var response = await client.PostAsJsonAsync("/api/v1/listings", new
        {
            title = "純 JSON 不應成功",
            description = "x",
            categoryCode = 1,
            conditionCode = 1,
            price = 100,
            residenceCode = 2,
            pickupLocationCode = 3
        });

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("UNSUPPORTED_MEDIA_TYPE", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostListingImage_UploadsAndReturnsBlobName()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "圖片上傳測試");
        using var formData = BuildImageMultipartContent("test-image.jpg", "image/jpeg", [1, 2, 3, 4, 5]);

        var response = await client.PostAsync($"/api/v1/listings/{id}/images", formData);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        var blobName = body.GetProperty("data").GetProperty("blobName").GetString();
        Assert.NotNull(blobName);
        Assert.StartsWith($"listings/{id}/1-", blobName, StringComparison.Ordinal);

        var detailResponse = await client.GetAsync($"/api/v1/listings/{id}");
        var detailBody = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        var imageUrls = detailBody.GetProperty("data").GetProperty("imageUrls");
        Assert.Equal(2, imageUrls.GetArrayLength());
        Assert.Equal(
            $"https://blob.local.test/listing/{blobName}",
            imageUrls[1].GetString());
    }

    [Fact]
    public async Task PostListingImage_AsNonOwner_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ListingApiFactory.OtherConfirmedUserName, UserPassword);
        using var formData = BuildImageMultipartContent("forbidden.jpg", "image/jpeg", [1, 2, 3]);

        var response = await client.PostAsync($"/api/v1/listings/{SeededTesterListingId}/images", formData);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("LISTING_ACCESS_DENIED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostListingImage_InvalidContentType_ReturnsBadRequest()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);
        using var formData = BuildImageMultipartContent("bad.txt", "text/plain", [1, 2, 3]);

        var response = await client.PostAsync($"/api/v1/listings/{SeededTesterListingId}/images", formData);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PutListing_WhenMissing_ReturnsNotFound()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var request = new
        {
            title = "更新測試",
            description = "更新內容",
            categoryCode = 3,
            conditionCode = 1,
            price = 900,
            residenceCode = 1,
            pickupLocationCode = 3
        };

        var response = await client.PutAsJsonAsync($"/api/v1/listings/{Guid.NewGuid()}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("LISTING_NOT_FOUND", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PutListing_AsNonOwner_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ListingApiFactory.OtherConfirmedUserName, UserPassword);

        var request = new
        {
            title = "非賣家更新",
            description = "不應成功",
            categoryCode = 1,
            conditionCode = 1,
            price = 1,
            residenceCode = 1,
            pickupLocationCode = 3
        };

        var response = await client.PutAsJsonAsync($"/api/v1/listings/{SeededTesterListingId}", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("LISTING_ACCESS_DENIED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteListing_AsNonOwner_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ListingApiFactory.OtherConfirmedUserName, UserPassword);

        var response = await client.DeleteAsync($"/api/v1/listings/{SeededTesterListingId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("LISTING_ACCESS_DENIED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PatchReserveListing_AsNonOwner_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ListingApiFactory.OtherConfirmedUserName, UserPassword);

        var response = await client.PatchAsync($"/api/v1/listings/{SeededTesterListingId}/reserve", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("LISTING_ACCESS_DENIED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteListing_DeletesResource()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var createRequest = new
        {
            title = "待刪除商品",
            description = "測試刪除",
            categoryCode = 5,
            conditionCode = 2,
            price = 500,
            residenceCode = 3,
            pickupLocationCode = 3
        };

        using var createForm = BuildCreateListingForm(createRequest);
        var createResponse = await client.PostAsync("/api/v1/listings", createForm);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("data").GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync($"/api/v1/listings/{id}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var deleteBody = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(deleteBody.GetProperty("success").GetBoolean());
        Assert.True(deleteBody.GetProperty("data").GetProperty("deleted").GetBoolean());
    }

    [Fact]
    public async Task PatchReserveListing_FromActive_ReturnsOk()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "狀態測試-ActiveToReserved");

        var response = await client.PatchAsync($"/api/v1/listings/{id}/reserve", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());

        var getResponse = await client.GetAsync($"/api/v1/listings/{id}");
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, getBody.GetProperty("data").GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task PatchReserveListing_WhenMissing_ReturnsNotFound()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var response = await client.PatchAsync($"/api/v1/listings/{Guid.NewGuid()}/reserve", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("LISTING_NOT_FOUND", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PatchSetListingInactive_FromActive_ReturnsOkAndStatusInactive()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "狀態測試-ActiveToInactive");

        var response = await client.PatchAsync($"/api/v1/listings/{id}/inactive", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        var data = body.GetProperty("data");
        var warn = data.GetProperty("warning");
        Assert.True(warn.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                      string.IsNullOrEmpty(warn.GetString()));

        var getResponse = await client.GetAsync($"/api/v1/listings/{id}");
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, getBody.GetProperty("data").GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task PatchArchiveListing_AliasesInactive_ReturnsOk()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "狀態測試-ArchiveAlias");

        var response = await client.PatchAsync($"/api/v1/listings/{id}/archive", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var getResponse = await client.GetAsync($"/api/v1/listings/{id}");
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, getBody.GetProperty("data").GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task PatchMarkDonated_WhenNotFreeOrCharity_ReturnsBadRequest()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "狀態測試-捐贈擋錯");

        var response = await client.PatchAsync($"/api/v1/listings/{id}/donated", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LISTING_DONATED_NOT_APPLICABLE", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PatchMarkDonated_WhenCharityFlagSet_ReturnsOk()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "狀態測試-捐贈成功");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var entity = await db.Listings.FirstAsync(x => x.Id == id);
            entity.IsCharity = true;
            await db.SaveChangesAsync();
        }

        var response = await client.PatchAsync($"/api/v1/listings/{id}/donated", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var getResponse = await client.GetAsync($"/api/v1/listings/{id}");
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, getBody.GetProperty("data").GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task PatchMarkGivenOrTraded_WhenNotTradeable_ReturnsBadRequest()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "狀態測試-易物擋錯");

        var response = await client.PatchAsync($"/api/v1/listings/{id}/given-or-traded", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LISTING_TRADE_MARKING_NOT_APPLICABLE", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PatchMarkGivenOrTraded_WhenTradeableFlagSet_ReturnsOk()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "狀態測試-易物成功");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var entity = await db.Listings.FirstAsync(x => x.Id == id);
            entity.IsTradeable = true;
            await db.SaveChangesAsync();
        }

        var response = await client.PatchAsync($"/api/v1/listings/{id}/given-or-traded", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var getResponse = await client.GetAsync($"/api/v1/listings/{id}");
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, getBody.GetProperty("data").GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task PatchMarkSold_WhenConversationsExist_ReturnsWarning()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "售出-warning");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var now = DateTime.UtcNow;
            db.Conversations.Add(new Conversation
            {
                Id = Guid.NewGuid(),
                ListingId = id,
                Participant1Id = ConfirmedUserId,
                Participant2Id = OtherUserId,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        var response = await client.PatchAsync($"/api/v1/listings/{id}/sold", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var warning = body.GetProperty("data").GetProperty("warning").GetString();
        Assert.NotNull(warning);
        Assert.Contains("對話記錄", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PatchMarkSold_WritesSystemMessagePerConversation()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "售出-系統訊息");

        var conv1 = Guid.NewGuid();
        var conv2 = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var now = DateTime.UtcNow;
            db.Conversations.Add(new Conversation
            {
                Id = conv1,
                ListingId = id,
                Participant1Id = ConfirmedUserId,
                Participant2Id = OtherUserId,
                CreatedAt = now,
                UpdatedAt = now
            });
            db.Conversations.Add(new Conversation
            {
                Id = conv2,
                ListingId = id,
                Participant1Id = ConfirmedUserId,
                Participant2Id = UnconfirmedUserId,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        var response = await client.PatchAsync($"/api/v1/listings/{id}/sold", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var messages = await db.Messages
                .Where(m => m.ConversationId == conv1 || m.ConversationId == conv2)
                .ToListAsync();
            Assert.Equal(2, messages.Count);
            Assert.All(messages, m =>
            {
                Assert.Equal(ListingConversationNotifyService.SoldListingSystemMessageContent, m.Content);
                Assert.Equal(ConfirmedUserId, m.SenderId);
            });
        }
    }

    [Fact]
    public async Task PatchReserveListing_FromSold_ReturnsConflict()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "狀態測試-SoldToReserved");
        var soldResponse = await client.PatchAsync($"/api/v1/listings/{id}/sold", content: null);
        Assert.Equal(HttpStatusCode.OK, soldResponse.StatusCode);

        var response = await client.PatchAsync($"/api/v1/listings/{id}/reserve", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("LISTING_INVALID_STATUS_TRANSITION", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostListing_WhenEmailNotificationDisabled_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
            var user = await db.AspNetUsers.FirstAsync(x => x.Id == ConfirmedUserId);
            user.EmailNotificationEnabled = false;
            await db.SaveChangesAsync();
        }

        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        using var notifyForm = BuildCreateListingForm(new
        {
            title = "通知關閉測試",
            description = "x",
            categoryCode = 1,
            conditionCode = 1,
            price = 100,
            residenceCode = 2,
            pickupLocationCode = 3
        });
        var response = await client.PostAsync("/api/v1/listings", notifyForm);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LISTING_EMAIL_NOTIFICATION_REQUIRED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostListing_EleventhActive_ReturnsConflict()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        for (var i = 0; i < 5; i++)
        {
            await CreateListingAsync(client, $"上限測試-{i}");
        }

        using var eleventhForm = BuildCreateListingForm(new
        {
            title = "第11件應失敗",
            description = "x",
            categoryCode = 1,
            conditionCode = 1,
            price = 100,
            residenceCode = 2,
            pickupLocationCode = 3
        });
        var response = await client.PostAsync("/api/v1/listings", eleventhForm);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LISTING_MAX_ACTIVE_REACHED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetListings_OneCharQuery_DoesNotApplyKeywordFilter()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/listings?q=z&page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetMyListings_ReturnsOkWithSeededTitles()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var response = await client.GetAsync("/api/v1/listings/mine?page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("data").GetProperty("items");
        var titles = GetListingTitles(items);
        Assert.Contains("二手書櫃", titles);
        Assert.Contains("filter-test-charity-only", titles);
    }

    [Fact]
    public async Task PatchReactivate_FromInactive_ReturnsOk()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "重新上架測試");
        await client.PatchAsync($"/api/v1/listings/{id}/inactive", content: null);

        var response = await client.PatchAsync($"/api/v1/listings/{id}/reactivate", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var get = await client.GetAsync($"/api/v1/listings/{id}");
        var getBody = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, getBody.GetProperty("data").GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task PatchReactivate_FromActive_ReturnsBadRequest()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "重新上架擋錯");

        var response = await client.PatchAsync($"/api/v1/listings/{id}/reactivate", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LISTING_REACTIVATE_NOT_ALLOWED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostTopPin_SetsPinnedAndDecrementsCredits()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "置頂測試");
        var beforeCredits = await GetTopPinCreditsAsync(factory, ConfirmedUserId);

        var response = await client.PostAsync($"/api/v1/listings/{id}/top-pin", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var afterCredits = await GetTopPinCreditsAsync(factory, ConfirmedUserId);
        Assert.Equal(beforeCredits - 1, afterCredits);

        var get = await client.GetAsync($"/api/v1/listings/{id}");
        var getBody = await get.Content.ReadFromJsonAsync<JsonElement>();
        var data = getBody.GetProperty("data");
        Assert.True(data.GetProperty("isPinned").GetBoolean());
        Assert.True(data.TryGetProperty("pinnedEndDate", out var end) && end.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task PutListing_WhenSold_ReturnsForbidden()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "售出後不可編輯");
        await client.PatchAsync($"/api/v1/listings/{id}/sold", content: null);

        var response = await client.PutAsJsonAsync($"/api/v1/listings/{id}", new
        {
            title = "試改",
            description = "x",
            categoryCode = 1,
            conditionCode = 1,
            price = 1,
            residenceCode = 2,
            pickupLocationCode = 3
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LISTING_NOT_EDITABLE", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteListing_WhenSold_ReturnsConflict()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "售出後不可刪");
        await client.PatchAsync($"/api/v1/listings/{id}/sold", content: null);

        var response = await client.DeleteAsync($"/api/v1/listings/{id}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LISTING_DELETE_NOT_ALLOWED", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PutListing_WithImageUrlsToDelete_RemovesImage()
    {
        using var factory = new ListingApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();
        await AuthenticateAsAsync(client, ConfirmedUserName, UserPassword);

        var id = await CreateListingAsync(client, "刪圖測試");
        using var form = BuildImageMultipartContent("one.jpg", "image/jpeg", [1, 2, 3, 4]);
        var imgResponse = await client.PostAsync($"/api/v1/listings/{id}/images", form);
        imgResponse.EnsureSuccessStatusCode();
        var imgBody = await imgResponse.Content.ReadFromJsonAsync<JsonElement>();
        var blobName = imgBody.GetProperty("data").GetProperty("blobName").GetString()!;

        var putResponse = await client.PutAsJsonAsync($"/api/v1/listings/{id}", new
        {
            title = "刪圖測試",
            description = "d",
            categoryCode = 1,
            conditionCode = 1,
            price = 100,
            residenceCode = 2,
            pickupLocationCode = 3,
            isFree = false,
            isCharity = false,
            isTradeable = false,
            imageUrlsToDelete = new[] { blobName }
        });
        putResponse.EnsureSuccessStatusCode();

        var detail = await client.GetAsync($"/api/v1/listings/{id}");
        var detailBody = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, detailBody.GetProperty("data").GetProperty("imageUrls").GetArrayLength());
    }

    private static async Task<int> GetTopPinCreditsAsync(ListingApiFactory factory, string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeighborGoodsDbContext>();
        return await db.AspNetUsers.Where(u => u.Id == userId).Select(u => u.TopPinCredits).FirstAsync();
    }

    private static async Task<Guid> CreateListingAsync(HttpClient client, string title)
    {
        var createRequest = new
        {
            title,
            description = "狀態測試建立資料",
            categoryCode = 1,
            conditionCode = 1,
            price = 1000,
            residenceCode = 2,
            pickupLocationCode = 3
        };

        using var form = BuildCreateListingForm(createRequest);
        var createResponse = await client.PostAsync("/api/v1/listings", form);
        createResponse.EnsureSuccessStatusCode();
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        return createBody.GetProperty("data").GetProperty("id").GetGuid();
    }

    private static MultipartFormDataContent BuildCreateListingForm(object payload, int imageCount = 1)
    {
        var imageBytes = new byte[] { 1, 2, 3, 4, 5 };
        var form = new MultipartFormDataContent();
        var json = JsonSerializer.Serialize(payload, CamelCaseJson);
        form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "payload");
        for (var i = 0; i < imageCount; i++)
        {
            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            form.Add(fileContent, "images", $"img-{i}.jpg");
        }

        return form;
    }

    private static async Task AuthenticateAsAsync(HttpClient client, string userNameOrEmail, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail,
            password
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static HashSet<string> GetListingTitles(JsonElement itemsArray)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in itemsArray.EnumerateArray())
        {
            set.Add(item.GetProperty("title").GetString()!);
        }

        return set;
    }

    private static MultipartFormDataContent BuildImageMultipartContent(
        string fileName,
        string contentType,
        byte[] content)
    {
        var formData = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        formData.Add(fileContent, "file", fileName);
        return formData;
    }
}
