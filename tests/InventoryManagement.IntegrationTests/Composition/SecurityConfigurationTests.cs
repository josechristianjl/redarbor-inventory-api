using InventoryManagement.Api.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace InventoryManagement.IntegrationTests.Composition;

public class SecurityConfigurationTests
{
    private const string Issuer =
        "https://issuer.example.test/realms/inventory";
    private const string Audience = "inventory-api";

    [Theory]
    [InlineData("KEYCLOAK_AUTHORITY", null)]
    [InlineData("KEYCLOAK_AUTHORITY", " ")]
    [InlineData("KEYCLOAK_ISSUER", null)]
    [InlineData("KEYCLOAK_ISSUER", " ")]
    [InlineData("KEYCLOAK_AUDIENCE", null)]
    [InlineData("KEYCLOAK_AUDIENCE", " ")]
    public void Authentication_WithMissingConfiguration_ShouldThrow(
        string key,
        string? value)
    {
        var configuration = CreateConfiguration();
        configuration[key] = value;
        var services = new ServiceCollection();
        var environment = CreateEnvironment(Environments.Production);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInventoryAuthentication(
                configuration, environment));
        Assert.Equal(
            $"The configuration value '{key}' is required.",
            exception.Message);
    }

    [Theory]
    [InlineData("KEYCLOAK_AUTHORITY", "relative/path", "Development")]
    [InlineData("KEYCLOAK_ISSUER", "relative/path", "Development")]
    [InlineData("KEYCLOAK_AUTHORITY", "ftp://issuer.example.test", "Development")]
    [InlineData("KEYCLOAK_ISSUER", "ftp://issuer.example.test", "Development")]
    [InlineData("KEYCLOAK_AUTHORITY", "http://issuer.example.test", "Production")]
    [InlineData("KEYCLOAK_ISSUER", "http://issuer.example.test", "Production")]
    public void Authentication_WithInvalidUrl_ShouldThrow(
        string key,
        string value,
        string environmentName)
    {
        var configuration = CreateConfiguration();
        configuration[key] = value;
        var services = new ServiceCollection();
        var environment = CreateEnvironment(environmentName);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInventoryAuthentication(
                configuration, environment));
        Assert.Equal(
            $"The configuration value '{key}' contains an invalid URL.",
            exception.Message);
    }

    [Theory]
    [InlineData("Development", "http://localhost:8081/realms/inventory", false)]
    [InlineData("Production", Issuer, true)]
    public void Authentication_WithValidConfiguration_ShouldRegisterJwtBearer(
        string environmentName,
        string issuer,
        bool requireHttpsMetadata)
    {
        var configuration = CreateConfiguration();
        configuration["KEYCLOAK_AUTHORITY"] = issuer;
        configuration["KEYCLOAK_ISSUER"] = issuer;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInventoryAuthentication(
            configuration, CreateEnvironment(environmentName));
        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.Equal(issuer, options.Authority);
        Assert.Equal(Audience, options.Audience);
        Assert.Equal(issuer,
            options.TokenValidationParameters.ValidIssuer);
        Assert.Equal(Audience,
            options.TokenValidationParameters.ValidAudience);
        Assert.Equal(requireHttpsMetadata,
            options.RequireHttpsMetadata);
    }

    [Fact]
    public void Swagger_WithMissingIssuer_ShouldThrow()
    {
        var configuration = CreateConfiguration();
        configuration["KEYCLOAK_ISSUER"] = null;
        var services = new ServiceCollection();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInventorySwagger(configuration));
        Assert.Equal(
            "The configuration value 'KEYCLOAK_ISSUER' is required.",
            exception.Message);
    }

    [Fact]
    public async Task Swagger_WithMissingClientId_ShouldThrow()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
        builder.Configuration["KEYCLOAK_SWAGGER_CLIENT_ID"] = " ";
        await using var app = builder.Build();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            app.UseInventorySwagger());
        Assert.Equal(
            "The configuration value 'KEYCLOAK_SWAGGER_CLIENT_ID' is required.",
            exception.Message);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["KEYCLOAK_AUTHORITY"] = Issuer,
                    ["KEYCLOAK_ISSUER"] = Issuer,
                    ["KEYCLOAK_AUDIENCE"] = Audience,
                    ["KEYCLOAK_SWAGGER_CLIENT_ID"] = "inventory-swagger"
                })
            .Build();
    }

    private static IHostEnvironment CreateEnvironment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName)
            .Returns(name);

        return environment.Object;
    }
}
