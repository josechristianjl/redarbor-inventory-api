using Dapper;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.Domain.Exceptions;
using InventoryManagement.Infrastructure.Persistence.Repositories.Write;
using InventoryManagement.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.IntegrationTests.Persistence;

[Collection("SqlServer integration")]
public class InventoryMovementWriteRepositoryTests
    : IClassFixture<ReadRepositoryFixture>
{
    private readonly ReadRepositoryFixture _fixture;

    private static readonly DateTime MovementDate =
        new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
            .AddTicks(1234567);

    public InventoryMovementWriteRepositoryTests(
        ReadRepositoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(MovementType.Entry, 0, 10, 10)]
    [InlineData(MovementType.Entry, 7, 5, 12)]
    [InlineData(MovementType.Exit, 7, 3, 4)]
    [InlineData(MovementType.Exit, 7, 7, 0)]
    public async Task RegisterAsync_WithValidMovement_ShouldPersistStockAndHistory(
        MovementType type,
        int initialStock,
        int quantity,
        int expectedStock)
    {
        var repository = CreateRepository();
        var productId = await InsertProductAsync(initialStock);
        var movement = new InventoryMovement(
            productId, type, quantity, MovementDate);
        var id = await repository.RegisterAsync(
            movement, CancellationToken.None);
        Assert.True(id > 0);
        var persisted = await ReadMovementAsync(id);
        Assert.Equal(id, persisted.Id);
        Assert.Equal(productId, persisted.ProductId);
        Assert.Equal(type, persisted.Type);
        Assert.Equal(quantity, persisted.Quantity);
        Assert.Equal(MovementDate.Ticks, persisted.CreatedAt.Ticks);
        Assert.Equal(DateTimeKind.Utc, persisted.CreatedAt.Kind);
        int expectedCount = initialStock > 0 ? 2 : 1;
        await AssertStateAsync(
            productId, expectedStock, expectedCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 4)]
    public async Task RegisterAsync_WithInsufficientStock_ShouldPreserveState(
        int initialStock,
        int quantity)
    {
        var repository = CreateRepository();
        var productId = await InsertProductAsync(initialStock);
        var movement = new InventoryMovement(
            productId, MovementType.Exit, quantity, MovementDate);
        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            repository.RegisterAsync(movement, CancellationToken.None));
        Assert.Equal(DomainRuleCode.InsufficientStock, exception.Code);
        int expectedCount = initialStock > 0 ? 1 : 0;
        await AssertStateAsync(
            productId, initialStock, expectedCount);
    }

    [Fact]
    public async Task RegisterAsync_WhenStockWouldOverflow_ShouldPreserveState()
    {
        var repository = CreateRepository();
        var productId = await InsertProductAsync(int.MaxValue);
        var movement = new InventoryMovement(
            productId, MovementType.Entry, 1, MovementDate);
        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            repository.RegisterAsync(movement, CancellationToken.None));
        Assert.Equal(DomainRuleCode.StockLimitExceeded, exception.Code);
        await AssertStateAsync(productId, int.MaxValue, 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegisterAsync_WithUnavailableProduct_ShouldThrowNotFound(
        bool exists)
    {
        var repository = CreateRepository();
        var productId = exists
            ? await InsertProductAsync(0, false)
            : int.MaxValue;
        var movement = new InventoryMovement(
            productId, MovementType.Entry, 10, MovementDate);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            repository.RegisterAsync(movement, CancellationToken.None));
        Assert.Equal(PersistenceRuleCode.ProductNotFound, exception.Code);
        await using var context = _fixture.CreateContext();
        var count = await context.InventoryMovements
            .CountAsync(item => item.ProductId == productId);
        Assert.Equal(0, count);
        if (exists)
        {
            await AssertStateAsync(productId, 0, 0);
        }
    }

    [Fact]
    public async Task RegisterAsync_WithPersistedMovement_ShouldThrowException()
    {
        var repository = CreateRepository();
        await using var context = _fixture.CreateContext();
        var movement = await context.InventoryMovements
            .AsNoTracking()
            .Where(item => item.ProductId == 1)
            .OrderBy(item => item.Id)
            .FirstAsync();
        Assert.True(movement.Id > 0);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.RegisterAsync(movement, CancellationToken.None));
        await AssertStateAsync(1, 7, 2);
    }

    [Fact]
    public async Task RegisterAsync_WithNullMovement_ShouldThrowException()
    {
        var repository = CreateRepository();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.RegisterAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterAsync_WhenMovementInsertFails_ShouldRollbackStock()
    {
        var repository = CreateRepository();
        var productId = await InsertProductAsync(7);
        await AddTestConstraintAsync();
        try
        {
            var movement = new InventoryMovement(
                productId, MovementType.Entry, 123456, MovementDate);
            var exception = await Assert.ThrowsAsync<SqlException>(() =>
                repository.RegisterAsync(movement, CancellationToken.None));
            Assert.Equal(547, exception.Number);
            Assert.Contains("CK_Test_RejectMovement", exception.Message);
            await AssertStateAsync(productId, 7, 1);
        }
        finally
        {
            await RemoveTestConstraintAsync();
        }
    }

    private InventoryMovementWriteRepository CreateRepository()
    {
        return new InventoryMovementWriteRepository(
            _fixture.CreateConnectionFactory());
    }

    private async Task<InventoryMovement> ReadMovementAsync(int id)
    {
        await using var context = _fixture.CreateContext();

        return await context.InventoryMovements
            .AsNoTracking()
            .SingleAsync(movement => movement.Id == id);
    }

    private async Task AssertStateAsync(
        int productId,
        int expectedStock,
        int expectedMovementCount)
    {
        await using var context = _fixture.CreateContext();
        var stock = await context.Products
            .Where(product => product.Id == productId)
            .Select(product => product.Stock)
            .SingleAsync();
        var movementCount = await context.InventoryMovements
            .CountAsync(movement => movement.ProductId == productId);
        Assert.Equal(expectedStock, stock);
        Assert.Equal(expectedMovementCount, movementCount);
    }

    private async Task<int> InsertProductAsync(
        int stock,
        bool isActive = true)
    {
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            INSERT INTO dbo.Products
                (CategoryId, SKU, Name, Stock, IsActive)
            VALUES
                (1, @SKU, @Name, @Stock, @IsActive);
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
            SKU = $"TEST-{Guid.NewGuid():N}",
            Name = "Integration test product",
            Stock = stock,
            IsActive = isActive
        });
    }

    private async Task AddTestConstraintAsync()
    {
        const string sql = """
            ALTER TABLE dbo.InventoryMovements
            ADD CONSTRAINT CK_Test_RejectMovement
            CHECK (Quantity <> 123456);
            """;
        await ExecuteSqlAsync(sql);
    }

    private async Task RemoveTestConstraintAsync()
    {
        const string sql = """
            ALTER TABLE dbo.InventoryMovements
            DROP CONSTRAINT CK_Test_RejectMovement;
            """;
        await ExecuteSqlAsync(sql);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = await _fixture
            .CreateConnectionFactory().OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(sql);
    }
}
