using Microsoft.Data.SqlClient;

namespace InventoryManagement.Infrastructure.Persistence.Connections;

public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;
    public SqlConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }
    public async Task<SqlConnection> OpenAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
