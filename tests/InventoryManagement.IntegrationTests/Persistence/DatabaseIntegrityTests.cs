using Dapper;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.Domain.Parameters;
using InventoryManagement.Infrastructure;
using InventoryManagement.Infrastructure.Persistence;
using InventoryManagement.Infrastructure.Persistence.Connections;
using InventoryManagement.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagement.IntegrationTests.Persistence;

[Collection("SqlServer integration")]
public class DatabaseIntegrityTests
    : IClassFixture<ReadRepositoryFixture>
{
    private static readonly int[] PermissionDeniedErrorNumbers = [229, 230];

    private readonly ReadRepositoryFixture _fixture;

    public DatabaseIntegrityTests(ReadRepositoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ApplicationAccount_ShouldNotHaveAdministrativeRoles()
    {
        await using var provider = CreateProvider();
        var factory = provider.GetRequiredService<SqlConnectionFactory>();
        await using var connection =
            await factory.OpenAsync(CancellationToken.None);
        Assert.Equal("inventory_app",
            await connection.ExecuteScalarAsync<string>("SELECT SUSER_SNAME();"));
        Assert.Equal("inventory_app",
            await connection.ExecuteScalarAsync<string>("SELECT USER_NAME();"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT IS_SRVROLEMEMBER('sysadmin');"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT IS_ROLEMEMBER('db_owner');"));
    }

    [Fact]
    public async Task ApplicationAccount_ShouldExecuteCompleteInventoryWorkflow()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var categoryId = await CreateCategoryAsync(services);
        var productId = await CreateProductAsync(services, categoryId);
        await VerifyUpdatesAsync(services, categoryId, productId);
        await VerifyMovementsAsync(services, productId);
        await VerifyDeactivationAsync(services, categoryId, productId);
    }

    [Theory]
    [InlineData("UPDATE dbo.InventoryMovements SET Quantity = 99 WHERE Id = 1;")]
    [InlineData("DELETE FROM dbo.InventoryMovements WHERE Id = 1;")]
    [InlineData("DELETE FROM dbo.Products WHERE Id = 1;")]
    [InlineData("DELETE FROM dbo.Categories WHERE Id = 1;")]
    public async Task ApplicationAccount_ShouldRejectUnauthorizedOperations(
        string sql)
    {
        await using var provider = CreateProvider();
        var factory = provider.GetRequiredService<SqlConnectionFactory>();
        await using var connection =
            await factory.OpenAsync(CancellationToken.None);
        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            connection.ExecuteAsync(sql));
        Assert.Contains(exception.Number, PermissionDeniedErrorNumbers);
    }

    [Theory]
    [InlineData(
        "INSERT INTO dbo.Categories (Name) VALUES (N'   ');",
        547, "CK_Categories_Name_NotBlank")]
    [InlineData(
        "INSERT INTO dbo.Categories (Name) VALUES (N'ELECTRONICS');",
        2627, "UQ_Categories_Name")]
    [InlineData(
        "INSERT INTO dbo.Products (CategoryId, SKU, Name) VALUES (1, N'   ', N'Product');",
        547, "CK_Products_SKU_NotBlank")]
    [InlineData(
        "INSERT INTO dbo.Products (CategoryId, SKU, Name) VALUES (1, N'CHECK-SKU', N'   ');",
        547, "CK_Products_Name_NotBlank")]
    [InlineData(
        "INSERT INTO dbo.Products (CategoryId, SKU, Name, Stock) VALUES (1, N'CHECK-SKU', N'Product', -1);",
        547, "CK_Products_Stock_NonNegative")]
    [InlineData(
        "INSERT INTO dbo.Products (CategoryId, SKU, Name, Stock, IsActive) VALUES (1, N'CHECK-SKU', N'Product', 5, 0);",
        547, "CK_Products_Inactive_Stock")]
    [InlineData(
        "INSERT INTO dbo.Products (CategoryId, SKU, Name) VALUES (1, N'sku-001', N'Product');",
        2627, "UQ_Products_SKU")]
    [InlineData(
        "INSERT INTO dbo.Products (CategoryId, SKU, Name) VALUES (2147483647, N'CHECK-SKU', N'Product');",
        547, "FK_Products_Categories")]
    [InlineData(
        "INSERT INTO dbo.InventoryMovements (ProductId, Type, Quantity) VALUES (1, 1, 0);",
        547, "CK_InventoryMovements_Quantity")]
    [InlineData(
        "INSERT INTO dbo.InventoryMovements (ProductId, Type, Quantity) VALUES (1, 1, -1);",
        547, "CK_InventoryMovements_Quantity")]
    [InlineData(
        "INSERT INTO dbo.InventoryMovements (ProductId, Type, Quantity) VALUES (1, 3, 1);",
        547, "CK_InventoryMovements_Type")]
    [InlineData(
        "INSERT INTO dbo.InventoryMovements (ProductId, Type, Quantity) VALUES (2147483647, 1, 1);",
        547, "FK_InventoryMovements_Products")]

    public async Task DatabaseConstraints_ShouldRejectInvalidData(
        string sql,
        int expectedErrorNumber,
        string constraintName)
    {
        await using var provider = CreateProvider();
        var factory = provider.GetRequiredService<SqlConnectionFactory>();
        await using var connection =
            await factory.OpenAsync(CancellationToken.None);
        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            connection.ExecuteAsync(sql));
        Assert.Equal(expectedErrorNumber, exception.Number);
        Assert.Contains(constraintName, exception.Message);
    }

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddInventoryPersistence(
            _fixture.CreateApplicationConfiguration());

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private static async Task<int> CreateCategoryAsync(
        IServiceProvider services)
    {
        var repository = services
            .GetRequiredService<ICategoryWriteRepository>();
        var category = new Category($"Security-{Guid.NewGuid():N}");
        var id = await repository.CreateAsync(
            category, CancellationToken.None);
        Assert.True(id > 0);

        return id;
    }

    private static async Task<int> CreateProductAsync(
        IServiceProvider services,
        int categoryId)
    {
        var repository = services
            .GetRequiredService<IProductWriteRepository>();
        var details = new ProductDetails(
            $"TEST-{Guid.NewGuid():N}", "Laptop", categoryId);
        var id = await repository.CreateAsync(
            new Product(details), CancellationToken.None);
        Assert.True(id > 0);

        return id;
    }

    private static async Task VerifyUpdatesAsync(
        IServiceProvider services,
        int categoryId,
        int productId)
    {
        var categories = services
            .GetRequiredService<ICategoryWriteRepository>();
        var products = services
            .GetRequiredService<IProductWriteRepository>();
        var categoryName = $"Updated-{Guid.NewGuid():N}";
        await categories.UpdateAsync(
            categoryId, categoryName, "Description", CancellationToken.None);
        var details = new ProductDetails(
            $"TEST-{Guid.NewGuid():N}", "Monitor", categoryId);
        await products.UpdateAsync(
            productId, details, CancellationToken.None);
        await VerifyUpdatedDataAsync(
            services, categoryId, productId, categoryName);
    }

    private static async Task VerifyUpdatedDataAsync(
        IServiceProvider services,
        int categoryId,
        int productId,
        string categoryName)
    {
        var categories = services
            .GetRequiredService<ICategoryReadRepository>();
        var products = services
            .GetRequiredService<IProductReadRepository>();
        var category = await categories.GetByIdAsync(
            categoryId, CancellationToken.None);
        var product = await products.GetByIdAsync(
            productId, CancellationToken.None);
        Assert.NotNull(category);
        Assert.Equal(categoryName, category.Name);
        Assert.Equal("Description", category.Description);
        Assert.NotNull(product);
        Assert.Equal("Monitor", product.Name);
        Assert.Equal(categoryId, product.CategoryId);
        Assert.Equal(0, product.Stock);
    }

    private static async Task VerifyMovementsAsync(
        IServiceProvider services,
        int productId)
    {
        var movements = services
            .GetRequiredService<IInventoryMovementWriteRepository>();
        var products = services
            .GetRequiredService<IProductReadRepository>();
        var entry = new InventoryMovement(
            productId, MovementType.Entry, 10, DateTime.UtcNow);
        var entryId = await movements.RegisterAsync(
            entry, CancellationToken.None);
        var product = await products.GetByIdAsync(
            productId, CancellationToken.None);
        Assert.NotNull(product);
        Assert.Equal(10, product.Stock);
        var exit = new InventoryMovement(
            productId, MovementType.Exit, 10, DateTime.UtcNow);
        var exitId = await movements.RegisterAsync(
            exit, CancellationToken.None);
        Assert.True(entryId > 0);
        Assert.True(exitId > entryId);
        await VerifyMovementHistoryAsync(services, productId);
    }

    private static async Task VerifyMovementHistoryAsync(
        IServiceProvider services,
        int productId)
    {
        var context = services.GetRequiredService<InventoryDbContext>();
        var movements = await context.InventoryMovements.AsNoTracking()
            .Where(movement => movement.ProductId == productId)
            .OrderBy(movement => movement.Id)
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal(MovementType.Entry, movements[0].Type);
        Assert.Equal(MovementType.Exit, movements[1].Type);
        Assert.All(movements, movement =>
            Assert.Equal(10, movement.Quantity));
        var stock = await context.Products
            .Where(product => product.Id == productId)
            .Select(product => product.Stock)
            .SingleAsync();
        Assert.Equal(0, stock);
    }

    private static async Task VerifyDeactivationAsync(
        IServiceProvider services,
        int categoryId,
        int productId)
    {
        var products = services
            .GetRequiredService<IProductWriteRepository>();
        var categories = services
            .GetRequiredService<ICategoryWriteRepository>();
        await products.DeactivateAsync(
            productId, CancellationToken.None);
        await categories.DeactivateAsync(
            categoryId, CancellationToken.None);
        var context = services.GetRequiredService<InventoryDbContext>();
        var product = await context.Products.AsNoTracking()
            .SingleAsync(item => item.Id == productId);
        var category = await context.Categories.AsNoTracking()
            .SingleAsync(item => item.Id == categoryId);
        Assert.False(product.IsActive);
        Assert.False(category.IsActive);
        Assert.Equal(0, product.Stock);
        Assert.Equal(2, await context.InventoryMovements
            .CountAsync(movement => movement.ProductId == productId));
    }
}
