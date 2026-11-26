using InventoryManagement.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using InventoryManagement.Infrastructure.Persistence.Connections;
using System.Text;

namespace InventoryManagement.IntegrationTests.Fixtures;

public sealed class ReadRepositoryFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04")
            .WithPassword($"TestOnly!{Guid.NewGuid():N}")
            .Build();

    private readonly string _applicationPassword = $"TestOnly!{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ExecuteScriptAsync(
            "master",
            Path.Combine("database", "scripts", "001_create_database.sql"));
        await ExecuteScriptAsync(
            "InventoryManagement",
            Path.Combine("database", "scripts", "002_create_schema.sql"));
        await ConfigureApplicationUserAsync();
        await ExecuteScriptAsync(
            "InventoryManagement",
            Path.Combine("Persistence", "ReadRepositorySeed.sql"));
    }

    public InventoryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(GetConnectionString("InventoryManagement"))
            .Options;

        return new InventoryDbContext(options);
    }

    public IConfiguration CreateConfiguration()
    {
        var connection = new SqlConnectionStringBuilder(
            GetConnectionString("InventoryManagement"));
        var values = new Dictionary<string, string?>
        {
            ["SQL_SERVER_HOST"] = _container.Hostname,
            ["SQL_SERVER_PORT"] = _container.GetMappedPublicPort(1433)
                .ToString(CultureInfo.InvariantCulture),
            ["SQL_SERVER_DATABASE"] = connection.InitialCatalog,
            ["SQL_SERVER_USER"] = connection.UserID,
            ["SQL_SERVER_PASSWORD"] = connection.Password,
            ["SQL_SERVER_TRUST_SERVER_CERTIFICATE"] = "true"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private string GetConnectionString(string database)
    {
        var builder = new SqlConnectionStringBuilder(
            _container.GetConnectionString())
        {
            InitialCatalog = database
        };

        return builder.ConnectionString;
    }

    private async Task ExecuteScriptAsync(string database, string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
        var sql = await File.ReadAllTextAsync(path);
        await using var connection = new SqlConnection(
            GetConnectionString(database));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = 60
        };
        await command.ExecuteNonQueryAsync();
    }

    public SqlConnectionFactory CreateConnectionFactory()
    {
        return new SqlConnectionFactory(
            GetConnectionString("InventoryManagement"));
    }

    private async Task ConfigureApplicationUserAsync()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "database", "scripts", "003_create_permissions.sql");
        var script = await File.ReadAllTextAsync(path);
        var passwordHex = Convert.ToHexString(
            Encoding.ASCII.GetBytes(_applicationPassword));
        var batches = script
            .Replace("$(APP_PASSWORD_HEX)", passwordHex, StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\nGO\n", StringSplitOptions.RemoveEmptyEntries);
        await using var connection = new SqlConnection(
            GetConnectionString("master"));
        await connection.OpenAsync();
        foreach (var batch in batches)
        {
            await using var command = new SqlCommand(batch, connection)
            {
                CommandTimeout = 60
            };
            await command.ExecuteNonQueryAsync();
        }
    }

    public IConfiguration CreateApplicationConfiguration()
    {
        var configuration = CreateConfiguration();
        configuration["SQL_SERVER_USER"] = "inventory_app";
        configuration["SQL_SERVER_PASSWORD"] = _applicationPassword;

        return configuration;
    }
}
