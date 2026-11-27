using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using InventoryManagement.Api.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using static InventoryManagement.IntegrationTests.Http.HttpResponseAssertions;

namespace InventoryManagement.IntegrationTests.Http.Authentication;

public class JwtAuthenticationTests
{
    private const string Issuer = "https://issuer.example.test/realms/inventory";
    private const string Audience = "inventory-api";
    private const string RequiredScope = "inventory.access";
    private const string ProtectedPath = "/test/protected";

    [Theory]
    [InlineData("inventory.access")]
    [InlineData("profile inventory.access")]
    [InlineData("inventory.access other")]
    public async Task Request_WithValidTokenAndScope_ShouldReturnSuccess(
        string scope)
    {
        await AssertResponseAsync(
            "valid", scope, HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("inventory.access.read")]
    [InlineData("INVENTORY.ACCESS")]
    [InlineData("other")]
    public async Task Request_WithValidTokenWithoutRequiredScope_ShouldReturnForbidden(
        string? scope)
    {
        await AssertResponseAsync(
            "valid", scope, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("missing-token")]
    [InlineData("malformed")]
    [InlineData("unsigned")]
    [InlineData("wrong-signature")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("expired")]
    [InlineData("not-yet-valid")]
    [InlineData("missing-expiration")]
    [InlineData("wrong-algorithm")]
    [InlineData("audience-trailing-slash")]
    public async Task Request_WithInvalidToken_ShouldReturnUnauthorized(
        string scenario)
    {
        await AssertResponseAsync(
            scenario, RequiredScope, HttpStatusCode.Unauthorized);
    }

    private static async Task AssertResponseAsync(
        string scenario,
        string? scope,
        HttpStatusCode expectedStatus)
    {
        using var rsa = RSA.Create(2048);
        var privateKey = new RsaSecurityKey(rsa)
        {
            KeyId = "test-key"
        };
        var publicKey = new RsaSecurityKey(
            rsa.ExportParameters(false))
        {
            KeyId = "test-key"
        };
        await using var app = CreateApplication(publicKey);
        await app.StartAsync();
        using var client = app.GetTestClient();
        SetAuthorization(client, scenario, scope, privateKey);
        using var response = await client.GetAsync(ProtectedPath);
        await AssertAuthenticationResponseAsync(
            response, expectedStatus);
    }

    private static void SetAuthorization(
        HttpClient client,
        string scenario,
        string? scope,
        RsaSecurityKey signingKey)
    {
        if (scenario == "missing-token")
        {
            return;
        }
        var token = scenario == "malformed"
            ? "not-a-jwt"
            : CreateToken(scenario, scope, signingKey);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private static string CreateToken(
        string scenario,
        string? scope,
        RsaSecurityKey signingKey)
    {
        using var alternateRsa = scenario == "wrong-signature"
            ? RSA.Create(2048)
            : null;
        var key = alternateRsa is null
            ? signingKey
            : new RsaSecurityKey(alternateRsa)
            {
                KeyId = signingKey.KeyId
            };
        var now = DateTime.UtcNow;
        var credentials = CreateSigningCredentials(scenario, key);
        var token = new JwtSecurityToken(
            issuer: scenario == "wrong-issuer"
                ? "https://untrusted.example.test"
                : Issuer,
            audience: GetAudience(scenario),
            claims: CreateClaims(scope),
            notBefore: scenario == "not-yet-valid"
                ? now.AddMinutes(1)
                : now.AddMinutes(-10),
            expires: GetExpiration(scenario, now),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static SigningCredentials? CreateSigningCredentials(
        string scenario,
        SecurityKey key)
    {
        if (scenario == "unsigned")
        {
            return null;
        }
        var algorithm = scenario == "wrong-algorithm"
            ? SecurityAlgorithms.RsaSha384
            : SecurityAlgorithms.RsaSha256;

        return new SigningCredentials(key, algorithm);
    }

    private static string GetAudience(string scenario)
    {
        return scenario switch
        {
            "wrong-audience" => "another-api",
            "audience-trailing-slash" => Audience + "/",
            _ => Audience
        };
    }

    private static DateTime? GetExpiration(
        string scenario,
        DateTime now)
    {
        return scenario switch
        {
            "expired" => now.AddMinutes(-1),
            "missing-expiration" => null,
            _ => now.AddMinutes(5)
        };
    }

    private static List<Claim> CreateClaims(string? scope)
    {
        var claims = new List<Claim>
        {
            new("sub", "integration-test")
        };
        if (scope is not null)
        {
            claims.Add(new Claim("scope", scope));
        }

        return claims;
    }

    private static WebApplication CreateApplication(
        SecurityKey publicKey)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["KEYCLOAK_AUTHORITY"] = Issuer,
                ["KEYCLOAK_ISSUER"] = Issuer,
                ["KEYCLOAK_AUDIENCE"] = Audience
            });
        builder.Services.AddInventoryErrorHandling();
        builder.Services.AddInventoryAuthentication(
            builder.Configuration, builder.Environment);
        ConfigureTestMetadata(builder.Services, publicKey);
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet(ProtectedPath, () =>
            Results.Text("authorized"))
            .RequireAuthorization();

        return app;
    }

    private static void ConfigureTestMetadata(
        IServiceCollection services,
        SecurityKey publicKey)
    {
        var metadata = new OpenIdConnectConfiguration
        {
            Issuer = Issuer
        };
        metadata.SigningKeys.Add(publicKey);
        services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.ConfigurationManager =
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(
                        metadata);
            });
    }

    private static async Task AssertAuthenticationResponseAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        if (expectedStatus == HttpStatusCode.OK)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("authorized",
                await response.Content.ReadAsStringAsync());

            return;
        }
        var code = expectedStatus == HttpStatusCode.Unauthorized
            ? "Unauthorized"
            : "Forbidden";
        await AssertProblemAsync(response, expectedStatus, code);
        if (expectedStatus == HttpStatusCode.Unauthorized)
        {
            Assert.Contains(response.Headers.WwwAuthenticate,
                value => value.Scheme == "Bearer");
        }
    }
}
