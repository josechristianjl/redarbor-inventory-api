using System.Data;
using System.Diagnostics;
using Dapper;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.Domain.Exceptions;
using InventoryManagement.Domain.Parameters;
using InventoryManagement.Infrastructure.Persistence.Repositories.Write;
using InventoryManagement.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.IntegrationTests.Persistence;

[Collection("SqlServer integration")]
public class ConcurrentWriteRepositoryTests
    : IClassFixture<ReadRepositoryFixture>
{
    private static readonly int[] ExpectedTargetCategoryIds = [2, 3];

    private readonly ReadRepositoryFixture _fixture;

    private readonly CategoryWriteRepository _categories;

    private readonly ProductWriteRepository _products;

    private readonly InventoryMovementWriteRepository _movements;

    public ConcurrentWriteRepositoryTests(ReadRepositoryFixture fixture)
    {
        _fixture = fixture;
        var factory = fixture.CreateConnectionFactory();
        _categories = new CategoryWriteRepository(factory);
        _products = new ProductWriteRepository(factory);
        _movements = new InventoryMovementWriteRepository(factory);
    }

    [Fact]
    public async Task ConcurrentExits_ShouldPreventOverselling()
    {
        var productId = await CreateProductAsync(stock: 5);
        var results = await ExecuteBlockedExitsAsync(productId);
        Assert.Single(results, result => result is null);
        var failure = Assert.Single(results, result => result is not null);
        var exception = Assert.IsType<DomainRuleException>(failure);
        Assert.Equal(DomainRuleCode.InsufficientStock, exception.Code);
        await using var context = _fixture.CreateContext();
        var product = await context.Products.AsNoTracking()
            .SingleAsync(item => item.Id == productId);
        var exits = await context.InventoryMovements.AsNoTracking()
            .Where(item => item.ProductId == productId &&
                item.Type == MovementType.Exit)
            .ToListAsync();
        Assert.Equal(1, product.Stock);
        Assert.Equal(4, Assert.Single(exits).Quantity);
        await AssertInventoryConsistencyAsync(productId);
    }

    [Fact]
    public async Task ConcurrentEntries_ShouldPreserveEveryStockUpdate()
    {
        var productId = await CreateProductAsync();
        var operations = Enumerable.Range(0, 10)
            .Select<int, Func<Task>>(_ => () =>
                RegisterMovementAsync(productId, MovementType.Entry, 1))
            .ToArray();
        var results = await RunTogetherAsync(operations);
        Assert.All(results, result => Assert.Null(result));
        await using var context = _fixture.CreateContext();
        var product = await context.Products.AsNoTracking()
            .SingleAsync(item => item.Id == productId);
        var movements = await context.InventoryMovements.AsNoTracking()
            .Where(item => item.ProductId == productId)
            .ToListAsync();
        Assert.Equal(10, product.Stock);
        Assert.Equal(10, movements.Count);
        Assert.All(movements, movement =>
            Assert.Equal(MovementType.Entry, movement.Type));
        await AssertInventoryConsistencyAsync(productId);
    }

    [Fact]
    public async Task ConcurrentEntryAndDeactivation_ShouldPreserveProductState()
    {
        var productId = await CreateProductAsync();
        var results = await RunTogetherAsync(
            () => RegisterMovementAsync(productId, MovementType.Entry, 5),
            () => _products.DeactivateAsync(
                productId, CancellationToken.None));
        AssertSingleSuccess(results);
        await using var context = _fixture.CreateContext();
        var product = await context.Products.AsNoTracking()
            .SingleAsync(item => item.Id == productId);
        if (results[0] is null)
        {
            var exception = Assert.IsType<DomainRuleException>(results[1]);
            Assert.Equal(DomainRuleCode.ProductHasStock, exception.Code);
            Assert.True(product.IsActive);
            Assert.Equal(5, product.Stock);
        }
        else
        {
            AssertPersistenceFailure(
                results[0], PersistenceRuleCode.ProductNotFound);
            Assert.False(product.IsActive);
            Assert.Equal(0, product.Stock);
        }
        await AssertInventoryConsistencyAsync(productId);
    }

    [Fact]
    public async Task ConcurrentProductCreationAndCategoryDeactivation_ShouldPreserveIntegrity()
    {
        var categoryId = await CreateCategoryAsync();
        var sku = CreateUniqueSku();
        var product = new Product(
            new ProductDetails(sku, "Laptop", categoryId));
        var results = await RunTogetherAsync(
            () => _products.CreateAsync(product, CancellationToken.None),
            () => _categories.DeactivateAsync(
                categoryId, CancellationToken.None));
        AssertSingleSuccess(results);
        if (results[0] is null)
        {
            AssertPersistenceFailure(
                results[1], PersistenceRuleCode.CategoryHasActiveProducts);
        }
        await AssertCategoryCreationOutcomeAsync(
            categoryId, sku, results[0]);
    }

    [Fact]
    public async Task ConcurrentReassignmentAndCategoryDeactivation_ShouldPreserveIntegrity()
    {
        var productId = await CreateProductAsync(stock: 5);
        var categoryId = await CreateCategoryAsync();
        var details = new ProductDetails(
            CreateUniqueSku(), "Updated product", categoryId);
        var results = await RunTogetherAsync(
            () => _products.UpdateAsync(
                productId, details, CancellationToken.None),
            () => _categories.DeactivateAsync(
                categoryId, CancellationToken.None));
        AssertSingleSuccess(results);
        if (results[0] is null)
        {
            AssertPersistenceFailure(
                results[1], PersistenceRuleCode.CategoryHasActiveProducts);
        }
        else
        {
            AssertPersistenceFailure(
                results[0], PersistenceRuleCode.CategoryNotFound);
        }
        await AssertReassignmentOutcomeAsync(
            productId, categoryId, results[0] is null);
    }

    [Fact]
    public async Task ConcurrentCategoryChanges_ShouldPreserveStockAndRelationships()
    {
        var productId = await CreateProductAsync(stock: 5);
        var firstDetails = new ProductDetails(
            CreateUniqueSku(), "First update", 2);
        var secondDetails = new ProductDetails(
            CreateUniqueSku(), "Second update", 3);
        var results = await RunTogetherAsync(
            () => _products.UpdateAsync(
                productId, firstDetails, CancellationToken.None),
            () => _products.UpdateAsync(
                productId, secondDetails, CancellationToken.None));
        Assert.All(results, result => Assert.Null(result));
        await using var context = _fixture.CreateContext();
        var product = await context.Products.AsNoTracking()
            .SingleAsync(item => item.Id == productId);
        Assert.Contains(product.CategoryId, ExpectedTargetCategoryIds);
        var expected = product.CategoryId == 2
            ? firstDetails : secondDetails;
        Assert.Equal(expected.Sku, product.SKU);
        Assert.Equal(expected.Name, product.Name);
        Assert.Equal(5, product.Stock);
        await AssertInventoryConsistencyAsync(productId);
    }

    [Fact]
    public async Task ConcurrentDuplicateSkuCreation_ShouldPersistOnlyOneProduct()
    {
        var sku = CreateUniqueSku();
        var first = new Product(
            new ProductDetails(sku, "First product", 1));
        var second = new Product(
            new ProductDetails(sku.ToUpperInvariant(), "Second product", 2));
        var results = await RunTogetherAsync(
            () => _products.CreateAsync(first, CancellationToken.None),
            () => _products.CreateAsync(second, CancellationToken.None));
        AssertSingleSuccess(results);
        var failure = Assert.Single(results, result => result is not null);
        AssertPersistenceFailure(failure, PersistenceRuleCode.DuplicateSku);
        await using var context = _fixture.CreateContext();
        var products = await context.Products.AsNoTracking()
            .Where(product => product.SKU == sku)
            .ToListAsync();
        Assert.Equal(0, Assert.Single(products).Stock);
    }

    private async Task<Exception?[]> ExecuteBlockedExitsAsync(int productId)
    {
        await using var connection = await _fixture
            .CreateConnectionFactory().OpenAsync(CancellationToken.None);
        await using var transaction = (SqlTransaction)
            await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await AcquireProductLockAsync(transaction, productId);
        var operations = RunTogetherAsync(
            () => RegisterMovementAsync(productId, MovementType.Exit, 4),
            () => RegisterMovementAsync(productId, MovementType.Exit, 4));
        bool observedContention;
        try
        {
            observedContention = await WaitForBlockedRequestsAsync();
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await operations;
        }
        Assert.True(observedContention,
            "Both concurrent requests must wait for the product lock.");

        return await operations;
    }

    private static async Task AcquireProductLockAsync(
        SqlTransaction transaction,
        int productId)
    {
        const string sql = """
            SELECT Id
            FROM dbo.Products
                WITH (UPDLOCK, HOLDLOCK, INDEX(PK_Products))
            WHERE Id = @Id;
            """;
        await transaction.Connection!.QuerySingleAsync<int>(
            sql, new { Id = productId }, transaction);
    }

    private async Task<bool> WaitForBlockedRequestsAsync()
    {
        const string sql = """
            SELECT COUNT(*)
            FROM sys.dm_exec_requests
            WHERE database_id = DB_ID()
                AND blocking_session_id > 0
                AND wait_type LIKE N'LCK[_]M[_]%';
            """;
        await using var connection = await _fixture
            .CreateConnectionFactory().OpenAsync(CancellationToken.None);
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            var count = await connection.ExecuteScalarAsync<int>(sql);
            if (count >= 2)
            {
                return true;
            }
            await Task.Delay(100);
        }

        return false;
    }

    private static async Task<Exception?[]> RunTogetherAsync(
        params Func<Task>[] operations)
    {
        var start = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = operations
            .Select(operation => ExecuteOperationAsync(operation, start.Task))
            .ToArray();
        start.SetResult(true);

        return await Task.WhenAll(tasks);
    }

    private static async Task<Exception?> ExecuteOperationAsync(
        Func<Task> operation,
        Task startSignal)
    {
        await startSignal;
        try
        {
            await operation();

            return null;
        }
        catch (Exception exception)
            when (exception is DomainRuleException or PersistenceRuleException)
        {
            return exception;
        }
    }

    private static void AssertSingleSuccess(Exception?[] results)
    {
        Assert.Single(results, result => result is null);
        Assert.Single(results, result => result is not null);
    }

    private static void AssertPersistenceFailure(
        Exception? failure,
        PersistenceRuleCode expectedCode)
    {
        var exception = Assert.IsType<PersistenceRuleException>(failure);
        Assert.Equal(expectedCode, exception.Code);
    }

    private async Task AssertCategoryCreationOutcomeAsync(
        int categoryId,
        string sku,
        Exception? creationFailure)
    {
        await using var context = _fixture.CreateContext();
        var category = await context.Categories.AsNoTracking()
            .SingleAsync(item => item.Id == categoryId);
        var product = await context.Products.AsNoTracking()
            .SingleOrDefaultAsync(item => item.SKU == sku);
        if (creationFailure is null)
        {
            Assert.True(category.IsActive);
            Assert.NotNull(product);
            Assert.Equal(categoryId, product.CategoryId);
            Assert.True(product.IsActive);
            Assert.Equal(0, product.Stock);
        }
        else
        {
            AssertPersistenceFailure(
                creationFailure, PersistenceRuleCode.CategoryNotFound);
            Assert.False(category.IsActive);
            Assert.Null(product);
        }
    }

    private async Task AssertReassignmentOutcomeAsync(
        int productId,
        int categoryId,
        bool reassigned)
    {
        await using var context = _fixture.CreateContext();
        var category = await context.Categories.AsNoTracking()
            .SingleAsync(item => item.Id == categoryId);
        var product = await context.Products.AsNoTracking()
            .SingleAsync(item => item.Id == productId);
        Assert.Equal(reassigned, category.IsActive);
        Assert.Equal(reassigned ? categoryId : 1, product.CategoryId);
        Assert.True(product.IsActive);
        Assert.Equal(5, product.Stock);
        await AssertInventoryConsistencyAsync(productId);
    }

    private async Task AssertInventoryConsistencyAsync(int productId)
    {
        await using var context = _fixture.CreateContext();
        var stock = await context.Products
            .Where(product => product.Id == productId)
            .Select(product => product.Stock)
            .SingleAsync();
        var movements = await context.InventoryMovements.AsNoTracking()
            .Where(movement => movement.ProductId == productId)
            .ToListAsync();
        long expectedStock = movements.Sum(movement =>
            movement.Type == MovementType.Entry
                ? (long)movement.Quantity : -movement.Quantity);
        Assert.Equal(expectedStock, (long)stock);
        Assert.True(stock >= 0);
    }

    private async Task<int> CreateCategoryAsync()
    {
        var category = new Category($"Category-{Guid.NewGuid():N}");

        return await _categories.CreateAsync(
            category, CancellationToken.None);
    }

    private async Task<int> CreateProductAsync(int stock = 0)
    {
        var product = new Product(
            new ProductDetails(CreateUniqueSku(), "Laptop", 1));
        var id = await _products.CreateAsync(
            product, CancellationToken.None);
        if (stock > 0)
        {
            await RegisterMovementAsync(id, MovementType.Entry, stock);
        }

        return id;
    }

    private async Task RegisterMovementAsync(
        int productId,
        MovementType type,
        int quantity)
    {
        var movement = new InventoryMovement(
            productId, type, quantity, DateTime.UtcNow);
        await _movements.RegisterAsync(
            movement, CancellationToken.None);
    }

    private static string CreateUniqueSku()
    {
        return $"TEST-{Guid.NewGuid():N}";
    }
}
