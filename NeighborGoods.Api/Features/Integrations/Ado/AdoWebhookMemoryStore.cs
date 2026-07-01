using Microsoft.Extensions.Options;

namespace NeighborGoods.Api.Features.Integrations.Ado;

public sealed class AdoWebhookMemoryStore
{
    private readonly object _lock = new();
    private readonly LinkedList<AdoWebhookEventEntry> _events = new();
    private readonly int _maxEvents;

    public AdoWebhookMemoryStore(IOptions<AdoWebhookOptions> options)
    {
        _maxEvents = Math.Max(options.Value.MaxEvents, 1);
    }

    public Guid Add(string rawBody)
    {
        var entry = new AdoWebhookEventEntry(Guid.NewGuid(), DateTime.UtcNow, rawBody);
        lock (_lock)
        {
            _events.AddLast(entry);
            while (_events.Count > _maxEvents)
            {
                _events.RemoveFirst();
            }
        }

        return entry.Id;
    }

    public AdoWebhookEventEntry? GetById(Guid id)
    {
        lock (_lock)
        {
            return _events.FirstOrDefault(x => x.Id == id);
        }
    }

    public (IReadOnlyList<AdoWebhookEventEntry> Items, int TotalCount) List(int page, int pageSize)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        lock (_lock)
        {
            var totalCount = _events.Count;
            var items = _events
                .Reverse()
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToList();
            return (items, totalCount);
        }
    }
}
