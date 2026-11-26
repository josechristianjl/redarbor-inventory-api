using Dapper;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Parameters;
using InventoryManagement.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;

namespace InventoryManagement.Infrastructure.Persistence.Repositories.Write;

internal static class ProductLockReader
{
    internal static async Task<Product> GetActiveAsync(
        SqlTransaction transaction,
        int id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Id, CategoryId, SKU, Name,
                   Description, Stock, IsActive
            FROM dbo.Products
                WITH (UPDLOCK, HOLDLOCK, INDEX(PK_Products))
            WHERE Id = @Id;
            """;
        var connection = transaction.Connection!;
        var row = await connection.QuerySingleOrDefaultAsync<ProductRow>(
            new CommandDefinition(sql, new { Id = id },
                transaction, cancellationToken: cancellationToken));
        if (row is null || !row.IsActive)
        {
            throw new PersistenceRuleException(
                PersistenceRuleCode.ProductNotFound,
                "The active product was not found.");
        }

        return RestoreProduct(row);
    }
    private static Product RestoreProduct(ProductRow row)
    {
        var details = new ProductDetails(
            row.SKU, row.Name, row.CategoryId, row.Description);

        return Product.Restore(
            row.Id, details, row.Stock, row.IsActive);
    }
}
