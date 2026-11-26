using Dapper;
using Microsoft.Data.SqlClient;

namespace InventoryManagement.Infrastructure.Persistence.Repositories.Write;

internal static class CategoryLockReader
{
    internal static async Task<bool?> GetActiveStateAsync(
        SqlTransaction transaction,
        int id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT IsActive
            FROM dbo.Categories
                WITH (UPDLOCK, HOLDLOCK, INDEX(PK_Categories))
            WHERE Id = @Id;
            """;
        var connection = transaction.Connection!;

        return await connection.QuerySingleOrDefaultAsync<bool?>(
            new CommandDefinition(sql, new { Id = id },
                transaction, cancellationToken: cancellationToken));
    }
}
