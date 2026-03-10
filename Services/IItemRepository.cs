using redisPlayground.Models;

namespace redisPlayground.Services;

public interface IItemRepository
{
    Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Item> CreateAsync(Item item, CancellationToken cancellationToken = default);
    Task<Item?> UpdateAsync(Guid id, Item item, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
