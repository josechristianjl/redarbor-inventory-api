using Dapper;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Exceptions;
using InventoryManagement.Domain.Parameters;
using InventoryManagement.Infrastructure.Persistence.Repositories.Write;
using InventoryManagement.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.IntegrationTests.Persistence;

[Collection("SqlServer integration")]
public class ProductWriteRepositoryTests
    : IClassFixture<ReadRepositoryFixture>
{
    private readonly ReadRepositoryFixture _fixture;

    public ProductWriteRepositoryTests(ReadRepositoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAsync_WithValidProduct_ShouldPersistProduct()
    {
        var repository = CreateRepository();
        var sku = CreateUniqueSku();
        var product = new Product(new ProductDetails(
            $"  {sku}  ", "  Laptop  ", 1, "  Device  "));
        var id = await repository.CreateAsync(
            product, CancellationToken.None);
        var persisted = await ReadProductAsync(id);
        Assert.True(id > 0);
        Assert.Equal(id, persisted.Id);
        Assert.Equal(sku, persisted.SKU);
        Assert.Equal("Laptop", persisted.Name);
        Assert.Equal("Device", persisted.Description);
        Assert.Equal(1, persisted.CategoryId);
        Assert.Equal(0, persisted.Stock);
        Assert.True(persisted.IsActive);
    }

    [Theory]
    [InlineData("persisted")]
    [InlineData("inactive")]
    [InlineData("stock")]
    public async Task CreateAsync_WithInvalidState_ShouldThrowException(
        string state)
    {
        var repository = CreateRepository();
        var details = new ProductDetails(
            CreateUniqueSku(), "Laptop", 1);
        var product = state == "persisted"
            ? Product.Restore(25, details, 0, true)
            : new Product(details);
        if (state == "inactive")
        {
            product.Deactivate();
        }
        if (state == "stock")
        {
            product.RegisterEntry(10);
        }
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.CreateAsync(product, CancellationToken.None));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public async Task CreateAsync_WithUnavailableCategory_ShouldThrowNotFound(
        int categoryId)
    {
        var repository = CreateRepository();
        var product = new Product(new ProductDetails(
            CreateUniqueSku(), "Laptop", categoryId));
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            repository.CreateAsync(product, CancellationToken.None));
        Assert.Equal(PersistenceRuleCode.CategoryNotFound, exception.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateAsync_WithDuplicateSku_ShouldThrowConflict(
        bool isActive)
    {
        var repository = CreateRepository();
        var sku = CreateUniqueSku();
        await InsertProductAsync(sku, isActive: isActive);
        var product = new Product(new ProductDetails(
            sku.ToUpperInvariant(), "Laptop", 1));
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            repository.CreateAsync(product, CancellationToken.None));
        Assert.Equal(PersistenceRuleCode.DuplicateSku, exception.Code);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task UpdateAsync_WithValidDetails_ShouldPreserveStock(
        int categoryId)
    {
        var repository = CreateRepository();
        var id = await InsertProductAsync(
            CreateUniqueSku(), stock: 47);
        var sku = CreateUniqueSku();
        var details = new ProductDetails(
            $"  {sku}  ", "  Monitor  ", categoryId, "  Display  ");
        await repository.UpdateAsync(
            id, details, CancellationToken.None);
        var persisted = await ReadProductAsync(id);
        Assert.Equal(sku, persisted.SKU);
        Assert.Equal("Monitor", persisted.Name);
        Assert.Equal("Display", persisted.Description);
        Assert.Equal(categoryId, persisted.CategoryId);
        Assert.Equal(47, persisted.Stock);
        Assert.True(persisted.IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateAsync_WithDuplicateSku_ShouldPreserveOriginalData(
        bool duplicateIsActive)
    {
        var repository = CreateRepository();
        var originalSku = CreateUniqueSku();
        var duplicateSku = CreateUniqueSku();
        var id = await InsertProductAsync(originalSku, stock: 7);
        await InsertProductAsync(
            duplicateSku, isActive: duplicateIsActive);
        var details = new ProductDetails(
            duplicateSku.ToUpperInvariant(), "Monitor", 2, "Display");
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            repository.UpdateAsync(id, details, CancellationToken.None));
        Assert.Equal(PersistenceRuleCode.DuplicateSku, exception.Code);
        var persisted = await ReadProductAsync(id);
        Assert.Equal(originalSku, persisted.SKU);
        Assert.Equal("Integration test product", persisted.Name);
        Assert.Null(persisted.Description);
        Assert.Equal(1, persisted.CategoryId);
        Assert.Equal(7, persisted.Stock);
        Assert.True(persisted.IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateAsync_WithUnavailableProduct_ShouldThrowNotFound(
        bool exists)
    {
        var repository = CreateRepository();
        var id = exists
            ? await InsertProductAsync(CreateUniqueSku(), isActive: false)
            : int.MaxValue;
        var details = new ProductDetails(
            CreateUniqueSku(), "Monitor", 1);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            repository.UpdateAsync(id, details, CancellationToken.None));
        Assert.Equal(PersistenceRuleCode.ProductNotFound, exception.Code);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public async Task UpdateAsync_WithUnavailableCategory_ShouldPreserveProduct(
        int categoryId)
    {
        var repository = CreateRepository();
        var sku = CreateUniqueSku();
        var id = await InsertProductAsync(sku, stock: 7);
        var details = new ProductDetails(
            CreateUniqueSku(), "Monitor", categoryId);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            repository.UpdateAsync(id, details, CancellationToken.None));
        Assert.Equal(PersistenceRuleCode.CategoryNotFound, exception.Code);
        var persisted = await ReadProductAsync(id);
        Assert.Equal(sku, persisted.SKU);
        Assert.Equal(1, persisted.CategoryId);
        Assert.Equal(7, persisted.Stock);
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidDetails_ShouldPreserveProduct()
    {
        var repository = CreateRepository();
        var sku = CreateUniqueSku();
        var id = await InsertProductAsync(sku, stock: 7);
        var details = new ProductDetails(
            CreateUniqueSku(), new string('A', 151), 2);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.UpdateAsync(id, details, CancellationToken.None));
        var persisted = await ReadProductAsync(id);
        Assert.Equal(sku, persisted.SKU);
        Assert.Equal("Integration test product", persisted.Name);
        Assert.Equal(1, persisted.CategoryId);
        Assert.Equal(7, persisted.Stock);
    }

    [Fact]
    public async Task DeactivateAsync_WithoutStock_ShouldDeactivateProduct()
    {
        var repository = CreateRepository();
        var id = await InsertProductAsync(CreateUniqueSku());
        await repository.DeactivateAsync(id, CancellationToken.None);
        var persisted = await ReadProductAsync(id);
        Assert.Equal(id, persisted.Id);
        Assert.Equal(0, persisted.Stock);
        Assert.False(persisted.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_WithStock_ShouldPreserveProduct()
    {
        var repository = CreateRepository();
        var id = await InsertProductAsync(
            CreateUniqueSku(), stock: 10);
        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            repository.DeactivateAsync(id, CancellationToken.None));
        Assert.Equal(DomainRuleCode.ProductHasStock, exception.Code);
        var persisted = await ReadProductAsync(id);
        Assert.Equal(10, persisted.Stock);
        Assert.True(persisted.IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeactivateAsync_WithUnavailableProduct_ShouldThrowNotFound(
        bool exists)
    {
        var repository = CreateRepository();
        var id = exists
            ? await InsertProductAsync(CreateUniqueSku(), isActive: false)
            : int.MaxValue;
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            repository.DeactivateAsync(id, CancellationToken.None));
        Assert.Equal(PersistenceRuleCode.ProductNotFound, exception.Code);
    }

    private ProductWriteRepository CreateRepository()
    {
        return new ProductWriteRepository(
            _fixture.CreateConnectionFactory());
    }

    private async Task<Product> ReadProductAsync(int id)
    {
        await using var context = _fixture.CreateContext();

        return await context.Products
            .AsNoTracking()
            .SingleAsync(product => product.Id == id);
    }

    private async Task<int> InsertProductAsync(
        string sku,
        int stock = 0,
        bool isActive = true)
    {
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            INSERT INTO dbo.Products
                (CategoryId, SKU, Name, Stock, IsActive)
            VALUES
                (1, @Sku, @Name, @Stock, @IsActive);
            DECLARE @Id INT = CONVERT(INT, SCOPE_IDENTITY());
            IF @Stock > 0
            BEGIN
                INSERT INTO dbo.InventoryMovements
                    (ProductId, Type, Quantity)
                VALUES (@Id, 1, @Stock);
            END;
            COMMIT TRANSACTION;
            SELECT @Id;
            """;
        await using var connection = await _fixture
            .CreateConnectionFactory().OpenAsync(CancellationToken.None);

        return await connection.QuerySingleAsync<int>(sql, new
        {
            Sku = sku,
            Name = "Integration test product",
            Stock = stock,
            IsActive = isActive
        });
    }

    private static string CreateUniqueSku()
    {
        return $"TEST-{Guid.NewGuid():N}";
    }
}
