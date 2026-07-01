using Microsoft.Extensions.Options;
using NeighborGoods.Api.Features.Integrations.Ado;

namespace NeighborGoods.Api.Tests;

public sealed class AdoWebhookMemoryStoreTests
{
    [Fact]
    public void Add_WhenExceedingMaxEvents_RemovesOldestEntry()
    {
        var store = new AdoWebhookMemoryStore(Options.Create(new AdoWebhookOptions { MaxEvents = 2 }));

        store.Add("first");
        store.Add("second");
        store.Add("third");

        var (_, totalCount) = store.List(1, 10);
        Assert.Equal(2, totalCount);

        var items = store.List(1, 10).Items;
        Assert.DoesNotContain(items, x => x.RawBody == "first");
        Assert.Contains(items, x => x.RawBody == "second");
        Assert.Contains(items, x => x.RawBody == "third");
    }

    [Fact]
    public void List_ReturnsNewestFirst_WithPagination()
    {
        var store = new AdoWebhookMemoryStore(Options.Create(new AdoWebhookOptions { MaxEvents = 10 }));
        store.Add("one");
        store.Add("two");
        store.Add("three");

        var (page1Items, totalCount) = store.List(1, 2);
        Assert.Equal(3, totalCount);
        Assert.Equal(2, page1Items.Count);
        Assert.Equal("three", page1Items[0].RawBody);
        Assert.Equal("two", page1Items[1].RawBody);

        var (page2Items, _) = store.List(2, 2);
        Assert.Single(page2Items);
        Assert.Equal("one", page2Items[0].RawBody);
    }

    [Fact]
    public void GetById_ReturnsMatchingEntry()
    {
        var store = new AdoWebhookMemoryStore(Options.Create(new AdoWebhookOptions()));
        var id = store.Add("{\"eventType\":\"workitem.updated\"}");

        var entry = store.GetById(id);
        Assert.NotNull(entry);
        Assert.Equal("{\"eventType\":\"workitem.updated\"}", entry.RawBody);
    }
}
