using System.Net;
using System.Text.Json;
using InventoryManagement.Api.Configuration;
using InventoryManagement.Api.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InventoryManagement.IntegrationTests.Http.Documentation;

public class SwaggerDocumentationTests
{
    private const string Issuer =
        "http://localhost:8081/realms/inventory";
    private const string SchemeName = "oauth2";
    private const string RequiredScope = "inventory.access";

    [Fact]
    public async Task Document_ShouldDescribeOAuth2AuthorizationCodeFlow()
    {
        using var document = await GetSwaggerDocumentAsync();
        var root = document.RootElement;
        var scheme = root.GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty(SchemeName);
        Assert.Equal("oauth2",
            scheme.GetProperty("type").GetString());
        var flows = scheme.GetProperty("flows");
        var flow = flows.GetProperty("authorizationCode");
        Assert.Single(flows.EnumerateObject());
        Assert.Equal(
            $"{Issuer}/protocol/openid-connect/auth",
            flow.GetProperty("authorizationUrl").GetString());
        Assert.Equal(
            $"{Issuer}/protocol/openid-connect/token",
            flow.GetProperty("tokenUrl").GetString());
        Assert.True(flow.GetProperty("scopes")
            .TryGetProperty(RequiredScope, out _));
        var requirement = Assert.Single(
            root.GetProperty("security").EnumerateArray());
        var scope = Assert.Single(requirement
            .GetProperty(SchemeName).EnumerateArray());
        Assert.Equal(RequiredScope, scope.GetString());
    }

    [Theory]
    [InlineData("/api/v1/categories", "get", "200")]
    [InlineData("/api/v1/categories", "post", "201")]
    [InlineData("/api/v1/categories/{id}", "get", "200")]
    [InlineData("/api/v1/categories/{id}", "put", "204")]
    [InlineData("/api/v1/categories/{id}", "delete", "204")]
    [InlineData("/api/v1/products", "get", "200")]
    [InlineData("/api/v1/products", "post", "201")]
    [InlineData("/api/v1/products/{id}", "get", "200")]
    [InlineData("/api/v1/products/{id}", "put", "204")]
    [InlineData("/api/v1/products/{id}", "delete", "204")]
    [InlineData("/api/v1/inventory-movements", "post", "201")]
    public async Task Document_ShouldDescribeOperationResponses(
        string path,
        string method,
        string successStatus)
    {
        using var document = await GetSwaggerDocumentAsync();
        var responses = document.RootElement
            .GetProperty("paths")
            .GetProperty(path)
            .GetProperty(method)
            .GetProperty("responses");
        Assert.True(responses.TryGetProperty(
            successStatus, out _));
        AssertProblemResponse(responses, "400");
        AssertProblemResponse(responses, "401");
        AssertProblemResponse(responses, "403");
        AssertProblemResponse(responses, "500");
        if (method is "post" or "put")
        {
            AssertProblemResponse(responses, "415");
        }
    }

    [Fact]
    public async Task SwaggerUi_InDevelopment_ShouldBeAvailable()
    {
        await using var app = CreateApplication();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(
            "/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "text/html", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Inventory Management API", body);
        Assert.Contains("index.js", body);
        using var scriptResponse = await client.GetAsync(
            "/swagger/index.js");
        Assert.Equal(HttpStatusCode.OK, scriptResponse.StatusCode);
        Assert.Equal(
            "application/javascript",
            scriptResponse.Content.Headers.ContentType?.MediaType);
        var script = await scriptResponse.Content.ReadAsStringAsync();
        Assert.Contains("inventory-swagger", script);
        Assert.Contains("inventory.access", script);
        Assert.Contains(
            "\"usePkceWithAuthorizationCodeGrant\":true", script);
    }

    private static void AssertProblemResponse(
        JsonElement responses,
        string status)
    {
        Assert.True(responses.TryGetProperty(
            status, out var response));
        Assert.True(response.GetProperty("content")
            .TryGetProperty("application/problem+json", out _));
    }

    private static async Task<JsonDocument> GetSwaggerDocumentAsync()
    {
        await using var app = CreateApplication();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        return JsonDocument.Parse(body);
    }

    private static WebApplication CreateApplication()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["KEYCLOAK_ISSUER"] = Issuer,
                ["KEYCLOAK_SWAGGER_CLIENT_ID"] = "inventory-swagger"
            });
        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(
                manager => manager.ApplicationParts.Clear())
            .AddApplicationPart(typeof(CategoriesController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddInventorySwagger(
            builder.Configuration);
        var app = builder.Build();
        app.UseInventorySwagger();
        app.MapControllers();

        return app;
    }
}
