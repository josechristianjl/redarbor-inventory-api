using Dapper;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Infrastructure.Persistence.Connections;
using InventoryManagement.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;

namespace InventoryManagement.Infrastructure.Persistence.Repositories.Write;

public sealed class CategoryWriteRepository : ICategoryWriteRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public CategoryWriteRepository(SqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateAsync(
        Category category,
        CancellationToken cancellationToken)
    {
        ValidateNewCategory(category);
        const string sql = """
            INSERT INTO dbo.Categories (Name, Description)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Description);
            """;
        await using var connection =
            await _connectionFactory.OpenAsync(cancellationToken);
        try
        {
            return await connection.QuerySingleAsync<int>(
                new CommandDefinition(sql,
                    new { category.Name, category.Description },
                    cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (IsDuplicateName(exception))
        {
            throw CreateDuplicateNameException(exception);
        }
    }

    public async Task UpdateAsync(
        int id,
        string name,
        string? description,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)
            await connection.BeginTransactionAsync(cancellationToken);
        var category = await GetLockedCategoryAsync(
            transaction, id, cancellationToken);
        category.UpdateDetails(name, description);
        await UpdateCategoryAsync(transaction, category, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeactivateAsync(
        int id,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)
            await connection.BeginTransactionAsync(cancellationToken);
        var category = await GetLockedCategoryAsync(
            transaction, id, cancellationToken);
        await EnsureNoActiveProductsAsync(
            transaction, category.Id, cancellationToken);
        category.Deactivate();
        const string sql = """
            UPDATE dbo.Categories
            SET IsActive = @IsActive
            WHERE Id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { category.Id, category.IsActive },
            transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    private static void ValidateNewCategory(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (category.Id != 0 || !category.IsActive)
        {
            throw new ArgumentException(
                "Only new active categories can be created.",
                nameof(category));
        }
    }

    private static async Task<Category> GetLockedCategoryAsync(
        SqlTransaction transaction,
        int id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Id, Name, Description, IsActive
            FROM dbo.Categories
                WITH (UPDLOCK, HOLDLOCK, INDEX(PK_Categories))
            WHERE Id = @Id;
            """;
        var connection = transaction.Connection!;
        var row = await connection.QuerySingleOrDefaultAsync<CategoryRow>(
            new CommandDefinition(sql, new { Id = id },
                transaction, cancellationToken: cancellationToken));
        if (row is null || !row.IsActive)
        {
            throw new PersistenceRuleException(
                PersistenceRuleCode.CategoryNotFound,
                "The active category was not found.");
        }

        return Category.Restore(
            row.Id, row.Name, row.Description, row.IsActive);
    }

    private static async Task UpdateCategoryAsync(
        SqlTransaction transaction,
        Category category,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.Categories
            SET Name = @Name, Description = @Description
            WHERE Id = @Id;
            """;
        var connection = transaction.Connection!;
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                sql, new { category.Id, category.Name, category.Description },
                transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (IsDuplicateName(exception))
        {
            throw CreateDuplicateNameException(exception);
        }
    }

    private static async Task EnsureNoActiveProductsAsync(
        SqlTransaction transaction,
        int categoryId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM dbo.Products
                WHERE CategoryId = @CategoryId AND IsActive = 1
            )
            THEN 1 ELSE 0 END;
            """;
        var connection = transaction.Connection!;
        var hasActiveProducts = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { CategoryId = categoryId },
                transaction, cancellationToken: cancellationToken));
        if (hasActiveProducts == 1)
        {
            throw new PersistenceRuleException(
                PersistenceRuleCode.CategoryHasActiveProducts,
                "A category with active products cannot be deactivated.");
        }
    }

    private static bool IsDuplicateName(SqlException exception)
    {
        return (exception.Number is 2601 or 2627)
            && exception.Message.Contains(
                "UQ_Categories_Name", StringComparison.Ordinal);
    }

    private static PersistenceRuleException CreateDuplicateNameException(
        SqlException exception)
    {
        return new PersistenceRuleException(
            PersistenceRuleCode.DuplicateCategoryName,
            "A category with the same name already exists.",
            exception);
    }
}
