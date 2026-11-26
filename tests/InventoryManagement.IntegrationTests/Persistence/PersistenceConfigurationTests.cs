using InventoryManagement.Infrastructure;
using InventoryManagement.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagement.IntegrationTests.Persistence;

public class PersistenceConfigurationTests
{
    [Fact]
    public void Registration_WithValidConfiguration_ShouldConfigureDatabase()
    {
        var configuration = CreateConfiguration();
        var services = new ServiceCollection();
        services.AddInventoryPersistence(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();
        var connection = new SqlConnectionStringBuilder(
            context.Database.GetConnectionString());
        Assert.Equal("127.0.0.1,14330", connection.DataSource);
        Assert.Equal("InventoryManagement", connection.InitialCatalog);
        Assert.Equal("inventory_app", connection.UserID);
        Assert.Equal("TestOnly!$;Example", connection.Password);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, connection.Encrypt);
        Assert.False(connection.TrustServerCertificate);
        Assert.False(connection.PersistSecurityInfo);
    }
    [Fact]
    public void Registration_WithoutPassword_ShouldThrowException()
    {
        var configuration = CreateConfiguration();
        configuration["SQL_SERVER_PASSWORD"] = null;
        var services = new ServiceCollection();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInventoryPersistence(configuration));
        Assert.Contains("SQL_SERVER_PASSWORD", exception.Message);
    }
    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("invalid")]
    public void Registration_WithInvalidPort_ShouldThrowException(string port)
    {
        var configuration = CreateConfiguration();
        configuration["SQL_SERVER_PORT"] = port;
        var services = new ServiceCollection();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInventoryPersistence(configuration));
        Assert.Contains("SQL_SERVER_PORT", exception.Message);
    }
    private static IConfiguration CreateConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["SQL_SERVER_HOST"] = "127.0.0.1",
            ["SQL_SERVER_PORT"] = "14330",
            ["SQL_SERVER_DATABASE"] = "InventoryManagement",
            ["SQL_SERVER_USER"] = "inventory_app",
            ["SQL_SERVER_PASSWORD"] = "TestOnly!$;Example"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
