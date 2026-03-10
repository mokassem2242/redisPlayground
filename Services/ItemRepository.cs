using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using redisPlayground.Data;
using redisPlayground.Models;

namespace redisPlayground.Services;

/// <summary>
/// CRUD repository with Redis cache-aside over SQL Server:
/// - Read: check Redis first; on miss, load from SQL Server and set cache.
/// - Write: persist to SQL Server, then invalidate/update Redis cache.
/// </summary>
public class ItemRepository : IItemRepository
{
    private readonly AppDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ItemRepository> _logger;
    private readonly string _keyPrefix;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private const string AllItemsKey = "items:all";
    private static TimeSpan CacheExpiration => TimeSpan.FromMinutes(5);

    public ItemRepository(
        AppDbContext db,
        IDistributedCache cache,
        ILogger<ItemRepository> logger,
        IOptions<RedisOptions>? redisOptions = null)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
        _keyPrefix = redisOptions?.Value?.InstanceName ?? "RedisPlayground:";
    }

    private string CacheKey(Guid id) => $"{_keyPrefix}item:{id}";

    public async Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var key = _keyPrefix + AllItemsKey;
        var bytes = await _cache.GetAsync(key, cancellationToken);
        if (bytes is { Length: > 0 })
        {
            _logger.LogDebug("Cache HIT: {Key}", key);
            return JsonSerializer.Deserialize<List<Item>>(bytes, JsonOptions) ?? new List<Item>();
        }

        _logger.LogDebug("Cache MISS: {Key} - loading from SQL Server", key);
        var list = await _db.Items.OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        var json = JsonSerializer.SerializeToUtf8Bytes(list, JsonOptions);
        await _cache.SetAsync(key, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheExpiration }, cancellationToken);
        return list;
    }

    public async Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(id);
        var bytes = await _cache.GetAsync(key, cancellationToken);
        if (bytes is { Length: > 0 })
        {
            _logger.LogDebug("Cache HIT: {Key}", key);
            return JsonSerializer.Deserialize<Item>(bytes, JsonOptions);
        }

        _logger.LogDebug("Cache MISS: {Key} - loading from SQL Server", key);
        var item = await _db.Items.FindAsync([id], cancellationToken);
        if (item is null)
            return null;

        var json = JsonSerializer.SerializeToUtf8Bytes(item, JsonOptions);
        await _cache.SetAsync(key, json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheExpiration }, cancellationToken);
        return item;
    }

    public async Task<Item> CreateAsync(Item item, CancellationToken cancellationToken = default)
    {
        if (item.Id == Guid.Empty)
            item.Id = Guid.NewGuid();
        item.CreatedAt = DateTime.UtcNow;
        _db.Items.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        await InvalidateListCacheAsync(cancellationToken);
        await SetItemCacheAsync(item, cancellationToken);
        _logger.LogInformation("Created item {Id} in SQL Server", item.Id);
        return item;
    }

    public async Task<Item?> UpdateAsync(Guid id, Item item, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Items.FindAsync([id], cancellationToken);
        if (existing is null)
            return null;

        existing.Name = item.Name;
        existing.Description = item.Description;
        await _db.SaveChangesAsync(cancellationToken);

        var updated = new Item { Id = existing.Id, Name = existing.Name, Description = existing.Description, CreatedAt = existing.CreatedAt };
        await InvalidateListCacheAsync(cancellationToken);
        await SetItemCacheAsync(updated, cancellationToken);
        _logger.LogInformation("Updated item {Id} in SQL Server", id);
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Items.FindAsync([id], cancellationToken);
        if (existing is null)
            return false;

        _db.Items.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);

        await InvalidateListCacheAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        _logger.LogInformation("Deleted item {Id} from SQL Server", id);
        return true;
    }

    private async Task InvalidateListCacheAsync(CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(_keyPrefix + AllItemsKey, cancellationToken);
    }

    private async Task SetItemCacheAsync(Item item, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(item, JsonOptions);
        await _cache.SetAsync(CacheKey(item.Id), json, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheExpiration }, cancellationToken);
    }
}
