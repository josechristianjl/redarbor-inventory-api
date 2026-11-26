using InventoryManagement.Application.Models;
using InventoryManagement.Infrastructure.Persistence.Repositories.Read;
using InventoryManagement.IntegrationTests.Fixtures;
using System.Data;
using Dapper;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Infrastructure;
using InventoryManagement.Infrastructure.Persistence.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagement.IntegrationTests.Persistence;

[Collection("SqlServer integration")]

public class ReadRepositoryTests : IClassFixture<ReadRepositoryFixture>
{
    private readonly ReadRepositoryFixture _fixture;
    public ReadRepositoryTests(ReadRepositoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetCategoryById_WithActiveCategory_ShouldReturnCategory()
    {
        await using var context = _fixture.CreateContext();
        var repository = new CategoryReadRepository(context);
        var result = await repository.GetByIdAsync(1, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Electronics", result.Name);
        Assert.Equal("Electronic devices", result.Description);
        Assert.True(result.IsActive);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(999)]
    public async Task GetCategoryById_WithUnavailableCategory_ShouldReturnNull(int id)
    {
        await using var context = _fixture.CreateContext();
        var repository = new CategoryReadRepository(context);
        var result = await repository.GetByIdAsync(id, CancellationToken.None);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(1, new int[] { 1, 2 })]
    [InlineData(2, new int[] { 3 })]
    [InlineData(3, new int[] { })]
    [InlineData(int.MaxValue, new int[] { })]
    public async Task GetCategories_WithPagination_ShouldReturnActiveCategories(
        int page,
        int[] expectedIds)
    {
        await using var context = _fixture.CreateContext();
        var repository = new CategoryReadRepository(context);
        var request = new PageRequest(page, 2);
        var result = await repository.GetPagedAsync(
            request, CancellationToken.None);
        Assert.Equal(page, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(expectedIds, result.Items.Select(item => item.Id));
        Assert.All(result.Items, item => Assert.True(item.IsActive));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetProductById_WithActiveProduct_ShouldReturnProduct()
    {
        await using var context = _fixture.CreateContext();
        var repository = new ProductReadRepository(context);
        var result = await repository.GetByIdAsync(1, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("SKU-001", result.Sku);
        Assert.Equal("Laptop", result.Name);
        Assert.Equal("Portable computer", result.Description);
        Assert.Equal(1, result.CategoryId);
        Assert.Equal(7, result.Stock);
        Assert.True(result.IsActive);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(999)]
    public async Task GetProductById_WithUnavailableProduct_ShouldReturnNull(int id)
    {
        await using var context = _fixture.CreateContext();
        var repository = new ProductReadRepository(context);
        var result = await repository.GetByIdAsync(id, CancellationToken.None);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(1, new int[] { 1, 2 })]
    [InlineData(2, new int[] { 3 })]
    [InlineData(3, new int[] { })]
    [InlineData(int.MaxValue, new int[] { })]
    public async Task GetProducts_WithPagination_ShouldReturnActiveProducts(
        int page,
        int[] expectedIds)
    {
        await using var context = _fixture.CreateContext();
        var repository = new ProductReadRepository(context);
        var request = new PageRequest(page, 2);
        var result = await repository.GetPagedAsync(
            request, CancellationToken.None);
        Assert.Equal(page, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(expectedIds, result.Items.Select(item => item.Id));
        Assert.All(result.Items, item => Assert.True(item.IsActive));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task PersistenceRegistration_ShouldConnectEfCoreAndDapper()
    {
        var services = new ServiceCollection();
        services.AddInventoryPersistence(_fixture.CreateConfiguration());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IProductReadRepository>();
        var result = await repository.GetPagedAsync(
            new PageRequest(), CancellationToken.None);
        Assert.Equal(3, result.TotalCount);
        var factory = scope.ServiceProvider
            .GetRequiredService<SqlConnectionFactory>();
        await using var connection =
            await factory.OpenAsync(CancellationToken.None);
        Assert.Equal(ConnectionState.Open, connection.State);
        var command = new CommandDefinition(
            "SELECT COUNT(*) FROM dbo.Products;",
            cancellationToken: CancellationToken.None);
        var total = await connection.ExecuteScalarAsync<int>(command);
        Assert.Equal(4, total);
    }
}
