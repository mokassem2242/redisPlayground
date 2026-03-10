using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using redisPlayground.Data;
using redisPlayground.Models;
using redisPlayground.Services;
using Xunit;

namespace redisPlayground.Tests;

public class ItemRepositoryTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ItemRepository CreateRepository(
        AppDbContext? db = null,
        IDistributedCache? cache = null,
        string keyPrefix = "Test:")
    {
        db ??= CreateDbContext();
        cache ??= new Mock<IDistributedCache>().Object;
        var logger = new Mock<ILogger<ItemRepository>>().Object;
        var redisOptions = Options.Create(new RedisOptions { InstanceName = keyPrefix });
        return new ItemRepository(db, cache, logger, redisOptions);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    [Fact]
    public async Task GetAllAsync_WhenCacheEmpty_LoadsFromDbAndReturnsItems()
    {
        var db = CreateDbContext();
        var item = new Item { Id = Guid.NewGuid(), Name = "A", Description = "Desc", CreatedAt = DateTime.UtcNow };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        byte[]? setBytes = null;
        string? setKey = null;
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((k, b, _, _) => { setKey = k; setBytes = b; })
            .Returns(Task.CompletedTask);

        var repo = CreateRepository(db: db, cache: cacheMock.Object, keyPrefix: "Test:");
        var result = await repo.GetAllAsync();

        Assert.Single(result);
        Assert.Equal("A", result[0].Name);
        Assert.NotNull(setKey);
        Assert.True(setKey!.Contains("items:all"));
        Assert.NotNull(setBytes);
        var deserialized = JsonSerializer.Deserialize<List<Item>>(setBytes!, JsonOptions);
        Assert.NotNull(deserialized);
        Assert.Single(deserialized);
    }

    [Fact]
    public async Task GetAllAsync_WhenCacheHasData_ReturnsCachedItems()
    {
        var cached = new List<Item> { new() { Id = Guid.NewGuid(), Name = "Cached", Description = "D", CreatedAt = DateTime.UtcNow } };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cached, JsonOptions);
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        var repo = CreateRepository(cache: cacheMock.Object);
        var result = await repo.GetAllAsync();

        Assert.Single(result);
        Assert.Equal("Cached", result[0].Name);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        var repo = CreateRepository();
        var result = await repo.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCacheMiss_LoadsFromDbAndCaches()
    {
        var db = CreateDbContext();
        var id = Guid.NewGuid();
        var item = new Item { Id = id, Name = "One", Description = "D1", CreatedAt = DateTime.UtcNow };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        byte[]? setBytes = null;
        cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, b, _, _) => setBytes = b)
            .Returns(Task.CompletedTask);

        var repo = CreateRepository(db: db, cache: cacheMock.Object);
        var result = await repo.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("One", result!.Name);
        Assert.NotNull(setBytes);
        var deserialized = JsonSerializer.Deserialize<Item>(setBytes!, JsonOptions);
        Assert.NotNull(deserialized);
        Assert.Equal(id, deserialized.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCacheHit_ReturnsCachedItem()
    {
        var id = Guid.NewGuid();
        var cached = new Item { Id = id, Name = "Cached", Description = "D", CreatedAt = DateTime.UtcNow };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cached, JsonOptions);
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        var repo = CreateRepository(cache: cacheMock.Object);
        var result = await repo.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("Cached", result!.Name);
    }

    [Fact]
    public async Task CreateAsync_PersistsItemAndReturnsWithId()
    {
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var db = CreateDbContext();
        var repo = CreateRepository(db: db, cache: cacheMock.Object);
        var item = new Item { Id = Guid.Empty, Name = "New", Description = "NewDesc" };

        var created = await repo.CreateAsync(item);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("New", created.Name);
        Assert.True(created.CreatedAt <= DateTime.UtcNow && created.CreatedAt > DateTime.UtcNow.AddSeconds(-2));
        var fromDb = await db.Items.FindAsync(created.Id);
        Assert.NotNull(fromDb);
        Assert.Equal("New", fromDb.Name);
    }

    [Fact]
    public async Task UpdateAsync_WhenExists_UpdatesAndReturns()
    {
        var db = CreateDbContext();
        var id = Guid.NewGuid();
        db.Items.Add(new Item { Id = id, Name = "Old", Description = "OldD", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var repo = CreateRepository(db: db, cache: cacheMock.Object);
        var updated = await repo.UpdateAsync(id, new Item { Name = "Updated", Description = "NewDesc" });

        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
        Assert.Equal("NewDesc", updated.Description);
        var fromDb = await db.Items.FindAsync(id);
        Assert.NotNull(fromDb);
        Assert.Equal("Updated", fromDb.Name);
    }

    [Fact]
    public async Task UpdateAsync_WhenNotExists_ReturnsNull()
    {
        var repo = CreateRepository();
        var result = await repo.UpdateAsync(Guid.NewGuid(), new Item { Name = "X", Description = "Y" });
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_WhenExists_RemovesAndReturnsTrue()
    {
        var db = CreateDbContext();
        var id = Guid.NewGuid();
        db.Items.Add(new Item { Id = id, Name = "ToDelete", Description = "D", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var cacheMock = new Mock<IDistributedCache>();
        cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var repo = CreateRepository(db: db, cache: cacheMock.Object);
        var deleted = await repo.DeleteAsync(id);

        Assert.True(deleted);
        var fromDb = await db.Items.FindAsync(id);
        Assert.Null(fromDb);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ReturnsFalse()
    {
        var repo = CreateRepository();
        var deleted = await repo.DeleteAsync(Guid.NewGuid());
        Assert.False(deleted);
    }
}
