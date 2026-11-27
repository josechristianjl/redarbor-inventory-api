using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using InventoryManagement.Api.Controllers;
using InventoryManagement.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace InventoryManagement.IntegrationTests.Http.EndToEnd;

internal sealed class InventoryApiFactory
    : WebApplicationFactory<CategoriesController>
{
    private const string Issuer =
        "https://issuer.example.test/realms/inventory";
    private const string Audience = "inventory-api";
    private const string KeyId = "full-host-test-key";
    private readonly Dictionary<string, string?> _settings;
    private readonly RsaSecurityKey _publicKey;

    internal InventoryApiFactory(
        ReadRepositoryFixture database,
        RSA signingRsa,
        string? databaseName = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(signingRsa);
        _settings = database.CreateApplicationConfiguration()
            .AsEnumerable()
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);

        if (databaseName is not null)
        {
            _settings["SQL_SERVER_DATABASE"] = databaseName;
        }

        _settings[HostDefaults.EnvironmentKey] =
            Environments.Production;
        _settings["KEYCLOAK_AUTHORITY"] = Issuer;
        _settings["KEYCLOAK_ISSUER"] = Issuer;
        _settings["KEYCLOAK_AUDIENCE"] = Audience;
        _settings["KEYCLOAK_SWAGGER_CLIENT_ID"] =
            "inventory-swagger";
        _publicKey = new RsaSecurityKey(
            signingRsa.ExportParameters(false))
        {
            KeyId = KeyId
        };
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(_settings);
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var metadata = new OpenIdConnectConfiguration
            {
                Issuer = Issuer
            };
            metadata.SigningKeys.Add(_publicKey);
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(
                            metadata);
                });
        });
    }

    internal HttpClient CreateHttpsClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    internal HttpClient CreateAuthenticatedClient(
        RSA signingRsa,
        string scope = "inventory.access")
    {
        var client = CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", CreateToken(signingRsa, scope));

        return client;
    }

    private static string CreateToken(RSA signingRsa, string scope)
    {
        var key = new RsaSecurityKey(signingRsa)
        {
            KeyId = KeyId
        };
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims:
            [
                new Claim("sub", "full-host-integration-test"),
                new Claim("scope", scope)
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                key, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
