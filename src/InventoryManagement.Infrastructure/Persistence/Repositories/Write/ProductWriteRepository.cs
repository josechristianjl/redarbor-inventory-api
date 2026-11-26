using System.Data;
using Dapper;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Parameters;
using InventoryManagement.Infrastructure.Persistence.Connections;
using Microsoft.Data.SqlClient;

namespace InventoryManagement.Infrastructure.Persistence.Repositories.Write;

public sealed class ProductWriteRepository : IProductWriteRepository
{
    private const int MaxUpdateAttempts = 3;

    private readonly SqlConnectionFactory _connectionFactory;

    public ProductWriteRepository(SqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        ValidateNewProduct(product);
        await using var connection =
            await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)
            await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken);
        var categoryState = await CategoryLockReader.GetActiveStateAsync(
            transaction, product.CategoryId, cancellationToken);
        EnsureActiveCategory(categoryState);
        var id = await InsertProductAsync(
            transaction, product, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return id;
    }

    public async Task UpdateAsync(
        int id,
        ProductDetails details,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(details.CategoryId);
        for (int attempt = 0; attempt < MaxUpdateAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryUpdateAsync(id, details, cancellationToken))
            {
                return;
            }
        }
        throw new PersistenceRuleException(
            PersistenceRuleCode.ConcurrentProductChange,
            "The product changed concurrently. Retry the operation.");
    }

    public async Task DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)
            await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken);
        var product = await ProductLockReader.GetActiveAsync(
            transaction, id, cancellationToken);
        product.Deactivate();
        const string sql = """
            UPDATE dbo.Products
            SET IsActive = @IsActive
            WHERE Id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { product.Id, product.IsActive },
            transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<bool> TryUpdateAsync(
        int id,
        ProductDetails details,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _connectionFactory.OpenAsync(cancellationToken);
        var originalCategoryId = await ReadCategoryIdAsync(
            connection, id, cancellationToken);
        await using var transaction = (SqlTransaction)
            await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken);
        var categoryState = await LockCategoriesAsync(
            transaction, originalCategoryId, details.CategoryId,
            cancellationToken);
        var product = await ProductLockReader.GetActiveAsync(
            transaction, id, cancellationToken);
        if (product.CategoryId != originalCategoryId)
        {
            return false;
        }
        EnsureActiveCategory(categoryState);
        product.UpdateDetails(details);
        await PersistDetailsAsync(transaction, product, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    private static async Task<int> ReadCategoryIdAsync(
        SqlConnection connection,
        int id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CategoryId
            FROM dbo.Products
            WHERE Id = @Id AND IsActive = 1;
            """;
        var categoryId = await connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(sql, new { Id = id },
                cancellationToken: cancellationToken));
        if (!categoryId.HasValue)
        {
            throw new PersistenceRuleException(
                PersistenceRuleCode.ProductNotFound,
                "The active product was not found.");
        }

        return categoryId.Value;
    }

    private static async Task<bool?> LockCategoriesAsync(
        SqlTransaction transaction,
        int originalId,
        int targetId,
        CancellationToken cancellationToken)
    {
        int firstId = Math.Min(originalId, targetId);
        int secondId = Math.Max(originalId, targetId);
        var firstState = await CategoryLockReader.GetActiveStateAsync(
            transaction, firstId, cancellationToken);
        if (firstId == secondId)
        {
            return firstState;
        }
        var secondState = await CategoryLockReader.GetActiveStateAsync(
            transaction, secondId, cancellationToken);

        return targetId == firstId ? firstState : secondState;
    }

    private static async Task<int> InsertProductAsync(
        SqlTransaction transaction,
        Product product,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.Products
                (CategoryId, SKU, Name, Description)
            OUTPUT INSERTED.Id
            VALUES
                (@CategoryId, @SKU, @Name, @Description);
            """;
        var connection = transaction.Connection!;
        try
        {
            return await connection.QuerySingleAsync<int>(
                new CommandDefinition(sql,
                    new
                    {
                        product.CategoryId,
                        product.SKU,
                        product.Name,
                        product.Description
                    },
                    transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (IsDuplicateSku(exception))
        {
            throw CreateDuplicateSkuException(exception);
        }
    }

    private static async Task PersistDetailsAsync(
        SqlTransaction transaction,
        Product product,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.Products
            SET CategoryId = @CategoryId,
                SKU = @SKU,
                Name = @Name,
                Description = @Description
            WHERE Id = @Id;
            """;
        var connection = transaction.Connection!;
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                sql, new
                {
                    product.Id,
                    product.CategoryId,
                    product.SKU,
                    product.Name,
                    product.Description
                },
                transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (IsDuplicateSku(exception))
        {
            throw CreateDuplicateSkuException(exception);
        }
    }

    private static void ValidateNewProduct(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (product.Id != 0 || !product.IsActive || product.Stock != 0)
        {
            throw new ArgumentException(
                "Only new active products with zero stock can be created.",
                nameof(product));
        }
    }

    private static void EnsureActiveCategory(bool? isActive)
    {
        if (isActive != true)
        {
            throw new PersistenceRuleException(
                PersistenceRuleCode.CategoryNotFound,
                "The active category was not found.");
        }
    }

    private static bool IsDuplicateSku(SqlException exception)
    {
        return (exception.Number is 2601 or 2627)
            && exception.Message.Contains(
                "UQ_Products_SKU", StringComparison.Ordinal);
    }

    private static PersistenceRuleException CreateDuplicateSkuException(
        SqlException exception)
    {
        return new PersistenceRuleException(
            PersistenceRuleCode.DuplicateSku,
            "A product with the same SKU already exists.",
            exception);
    }
}
