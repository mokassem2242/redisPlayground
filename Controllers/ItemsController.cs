using Microsoft.AspNetCore.Mvc;
using redisPlayground.Models;
using redisPlayground.Services;

namespace redisPlayground.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly IItemRepository _items;
    private readonly ILogger<ItemsController> _logger;

    public ItemsController(IItemRepository items, ILogger<ItemsController> logger)
    {
        _items = items;
        _logger = logger;
    }

    /// <summary>GET /api/items - List all items (cached in Redis).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Item>>> GetAll(CancellationToken cancellationToken)
    {
        var list = await _items.GetAllAsync(cancellationToken);
        return Ok(list);
    }

    /// <summary>GET /api/items/{id} - Get one item by id (cached in Redis).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Item>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _items.GetByIdAsync(id, cancellationToken);
        if (item is null)
            return NotFound();
        return Ok(item);
    }

    /// <summary>POST /api/items - Create item (invalidates list cache, caches new item).</summary>
    [HttpPost]
    public async Task<ActionResult<Item>> Create([FromBody] CreateItemRequest request, CancellationToken cancellationToken)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description
        };
        item = await _items.CreateAsync(item, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    /// <summary>PUT /api/items/{id} - Update item (invalidates list cache, updates item cache).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Item>> Update(Guid id, [FromBody] UpdateItemRequest request, CancellationToken cancellationToken)
    {
        var item = new Item { Name = request.Name, Description = request.Description };
        var updated = await _items.UpdateAsync(id, item, cancellationToken);
        if (updated is null)
            return NotFound();
        return Ok(updated);
    }

    /// <summary>DELETE /api/items/{id} - Delete item (invalidates list and item cache).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _items.DeleteAsync(id, cancellationToken);
        if (!deleted)
            return NotFound();
        return NoContent();
    }
}

public record CreateItemRequest(string Name, string? Description = null);
public record UpdateItemRequest(string Name, string? Description = null);
