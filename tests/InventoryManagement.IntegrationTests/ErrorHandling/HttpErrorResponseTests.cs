using System.Net;
using System.Text;
using System.Text.Json;
using InventoryManagement.Api.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InventoryManagement.IntegrationTests.ErrorHandling;

public class HttpErrorResponseTests
{
    [Theory]
    [InlineData(400, "InvalidRequest")]
    [InlineData(401, "Unauthorized")]
    [InlineData(403, "Forbidden")]
    [InlineData(500, "InternalServerError")]
    public async Task Request_WithEmptyErrorResponse_ShouldReturnProblemDetails(
        int status,
        string code)
    {
        await using var app = CreateApplication();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var path = $"/test/errors/status/{status}";
        using var response = await client.GetAsync(path);
        await AssertProblemAsync(response, status, code, path);
        if (status == StatusCodes.Status401Unauthorized)
        {
            Assert.Contains(response.Headers.WwwAuthenticate,
                value => value.Scheme == "Bearer");
        }
    }

    [Fact]
    public async Task Request_WithUnknownRoute_ShouldReturnNotFound()
    {
        await using var app = CreateApplication();
        await app.StartAsync();
        using var client = app.GetTestClient();
        const string path = "/test/errors/missing";
        using var response = await client.GetAsync(path);
        await AssertProblemAsync(
            response, 404, "NotFound", path);
    }

    [Fact]
    public async Task Request_WithUnsupportedMethod_ShouldReturnMethodNotAllowed()
    {
        await using var app = CreateApplication();
        await app.StartAsync();
        using var client = app.GetTestClient();
        const string path = "/test/errors/write";
        using var response = await client.GetAsync(path);
        await AssertProblemAsync(
            response, 405, "MethodNotAllowed", path);
        Assert.Contains("POST", response.Content.Headers.Allow);
    }

    [Fact]
    public async Task Request_WithUnsupportedContentType_ShouldReturnProblemDetails()
    {
        await using var app = CreateApplication();
        await app.StartAsync();
        using var client = app.GetTestClient();
        const string path = "/test/errors/write";
        using var content = new StringContent(
            "Unsupported content", Encoding.UTF8, "text/plain");
        using var response = await client.PostAsync(path, content);
        await AssertProblemAsync(
            response, 415, "UnsupportedMediaType", path);
    }

    [Theory]
    [InlineData("{\"name\":")]
    [InlineData("{\"sku\":\"SKU-001\",\"name\":true,\"categoryId\":1}")]
    [InlineData("{\"sku\":\"SKU-001\",\"name\":\"Laptop\",\"categoryId\":1,\"stock\":500}")]
    [InlineData("")]
    public async Task Request_WithInvalidJson_ShouldReturnValidationErrors(
        string json)
    {
        await using var app = CreateApplication();
        await app.StartAsync();
        using var client = app.GetTestClient();
        const string path = "/test/errors/write";
        using var content = new StringContent(
            json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(path, content);
        await AssertProblemAsync(
            response, 400, "InvalidRequest", path, expectErrors: true);
    }

    [Fact]
    public async Task Request_WithInvalidQueryParameter_ShouldReturnValidationErrors()
    {
        await using var app = CreateApplication();
        await app.StartAsync();
        using var client = app.GetTestClient();
        const string path = "/test/errors/query";
        using var response = await client.GetAsync(
            $"{path}?page=INTERNAL_TEST_MARKER");
        await AssertProblemAsync(
            response, 400, "InvalidRequest", path, expectErrors: true);
    }

    [Fact]
    public async Task Request_WithExistingErrorBody_ShouldPreserveResponse()
    {
        await using var app = CreateApplication();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/test/errors/body");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Existing response", body);
    }

    [Fact]
    public async Task Request_WithSuccessfulEmptyResponse_ShouldPreserveNoContent()
    {
        await using var app = CreateApplication();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/test/errors/empty");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(body);
    }

    private static WebApplication CreateApplication()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(HttpErrorProbeController).Assembly);
        builder.Services.AddInventoryErrorHandling();
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.MapControllers();
        app.MapGet("/test/errors/status/{status:int}",
            CreateStatusResponse);
        app.MapGet("/test/errors/body", () =>
            Results.Text("Existing response", statusCode: 409));
        app.MapGet("/test/errors/empty", () =>
            Results.NoContent());

        return app;
    }

    private static IResult CreateStatusResponse(
        int status,
        HttpContext context)
    {
        if (status == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        return Results.StatusCode(status);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        int status,
        string code,
        string path,
        bool expectErrors = false)
    {
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("INTERNAL_TEST_MARKER", body);
        using var document = JsonDocument.Parse(body);
        var problem = document.RootElement;
        Assert.Equal("about:blank",
            problem.GetProperty("type").GetString());
        Assert.Equal(status,
            problem.GetProperty("status").GetInt32());
        Assert.Equal(code,
            problem.GetProperty("code").GetString());
        Assert.Equal(path,
            problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("detail").GetString()));
        Assert.Equal(expectErrors,
            problem.TryGetProperty("errors", out var errors));
        if (expectErrors)
        {
            AssertValidationErrors(errors);
        }
    }

    private static void AssertValidationErrors(JsonElement errors)
    {
        Assert.NotEmpty(errors.EnumerateObject());
        Assert.All(errors.EnumerateObject(), error =>
        {
            Assert.Equal(1, error.Value.GetArrayLength());
            Assert.Equal("The supplied value is invalid.",
                error.Value[0].GetString());
        });
    }
}
