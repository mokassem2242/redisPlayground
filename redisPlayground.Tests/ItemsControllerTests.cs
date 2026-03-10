using Microsoft.AspNetCore.Mvc;
using Moq;
using redisPlayground.Controllers;
using redisPlayground.Models;
using redisPlayground.Services;
using Xunit;

namespace redisPlayground.Tests;

public class ItemsControllerTests
{
    private static ItemsController CreateController(IItemRepository? repository = null)
    {
        repository ??= new Mock<IItemRepository>().Object;
        var logger = new Mock<ILogger<ItemsController>>().Object;
        return new ItemsController(repository, logger);
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithList()
    {
        var items = new List<Item> { new() { Id = Guid.NewGuid(), Name = "A", CreatedAt = DateTime.UtcNow } };
        var repoMock = new Mock<IItemRepository>();
        repoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(items);
        var controller = CreateController(repoMock.Object);

        var result = await controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<Item>>(ok.Value);
        Assert.Single(list);
        Assert.Equal("A", list[0].Name);
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsOkWithItem()
    {
        var id = Guid.NewGuid();
        var item = new Item { Id = id, Name = "One", CreatedAt = DateTime.UtcNow };
        var repoMock = new Mock<IItemRepository>();
        repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        var controller = CreateController(repoMock.Object);

        var result = await controller.GetById(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(item, ok.Value);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var repoMock = new Mock<IItemRepository>();
        repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Item?)null);
        var controller = CreateController(repoMock.Object);

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtActionWithItem()
    {
        var createdItem = new Item { Id = Guid.NewGuid(), Name = "New", Description = "Desc", CreatedAt = DateTime.UtcNow };
        var repoMock = new Mock<IItemRepository>();
        repoMock.Setup(r => r.CreateAsync(It.IsAny<Item>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdItem);
        var controller = CreateController(repoMock.Object);
        var request = new CreateItemRequest("New", "Desc");

        var result = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ItemsController.GetById), created.ActionName);
        Assert.Equal(createdItem.Id, (created.RouteValues!["id"] as Guid?));
        Assert.Equal(createdItem, created.Value);
    }

    [Fact]
    public async Task Update_WhenFound_ReturnsOkWithItem()
    {
        var id = Guid.NewGuid();
        var updatedItem = new Item { Id = id, Name = "Updated", Description = "D", CreatedAt = DateTime.UtcNow };
        var repoMock = new Mock<IItemRepository>();
        repoMock.Setup(r => r.UpdateAsync(id, It.IsAny<Item>(), It.IsAny<CancellationToken>())).ReturnsAsync(updatedItem);
        var controller = CreateController(repoMock.Object);
        var request = new UpdateItemRequest("Updated", "D");

        var result = await controller.Update(id, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(updatedItem, ok.Value);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        var repoMock = new Mock<IItemRepository>();
        repoMock.Setup(r => r.UpdateAsync(It.IsAny<Guid>(), It.IsAny<Item>(), It.IsAny<CancellationToken>())).ReturnsAsync((Item?)null);
        var controller = CreateController(repoMock.Object);
        var request = new UpdateItemRequest("X", "Y");

        var result = await controller.Update(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_WhenExists_ReturnsNoContent()
    {
        var repoMock = new Mock<IItemRepository>();
        repoMock.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = CreateController(repoMock.Object);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WhenNotExists_ReturnsNotFound()
    {
        var repoMock = new Mock<IItemRepository>();
        repoMock.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = CreateController(repoMock.Object);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
