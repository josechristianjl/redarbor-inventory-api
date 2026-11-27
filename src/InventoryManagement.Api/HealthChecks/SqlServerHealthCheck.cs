using InventoryManagement.Infrastructure.Persistence.Connections;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InventoryManagement.Api.HealthChecks;

public sealed class SqlServerHealthCheck : IHealthCheck
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlServerHealthCheck(SqlConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";
        command.CommandTimeout = 5;
        await command.ExecuteScalarAsync(cancellationToken);

        return HealthCheckResult.Healthy();
    }
}
