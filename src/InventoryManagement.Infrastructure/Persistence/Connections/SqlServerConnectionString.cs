using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace InventoryManagement.Infrastructure.Persistence.Connections;

internal static class SqlServerConnectionString
{
    internal static string Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var host = GetRequiredValue(configuration, "SQL_SERVER_HOST");
        var port = GetPort(configuration);
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = FormattableString.Invariant($"{host},{port}"),
            InitialCatalog = GetRequiredValue(
                configuration, "SQL_SERVER_DATABASE"),
            UserID = GetRequiredValue(configuration, "SQL_SERVER_USER"),
            Password = GetRequiredValue(configuration, "SQL_SERVER_PASSWORD"),
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = GetTrustServerCertificate(configuration),
            PersistSecurityInfo = false,
            ConnectTimeout = 15
        };

        return builder.ConnectionString;
    }
    private static string GetRequiredValue(
        IConfiguration configuration,
        string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"The configuration value '{key}' is required.");
        }

        return value;
    }
    private static int GetPort(IConfiguration configuration)
    {
        var value = GetRequiredValue(configuration, "SQL_SERVER_PORT");
        if (!int.TryParse(value, NumberStyles.None,
                CultureInfo.InvariantCulture, out int port) ||
            port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "SQL_SERVER_PORT must be between 1 and 65535.");
        }

        return port;
    }
    private static bool GetTrustServerCertificate(
        IConfiguration configuration)
    {
        var value = configuration["SQL_SERVER_TRUST_SERVER_CERTIFICATE"]
            ?? "false";
        if (!bool.TryParse(value, out bool trustServerCertificate))
        {
            throw new InvalidOperationException(
                "SQL_SERVER_TRUST_SERVER_CERTIFICATE must be true or false.");
        }

        return trustServerCertificate;
    }
}
