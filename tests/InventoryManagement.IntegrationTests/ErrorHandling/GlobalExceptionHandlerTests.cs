using System.Net;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using InventoryManagement.Api.Configuration;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Domain.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace InventoryManagement.IntegrationTests.ErrorHandling;

public class GlobalExceptionHandlerTests
{
    private const string InternalMessage = "INTERNAL_TEST_MARKER";

    [Theory]
    [InlineData(PersistenceRuleCode.CategoryNotFound,
        HttpStatusCode.NotFound, "CategoryNotFound")]
    [InlineData(PersistenceRuleCode.ProductNotFound,
        HttpStatusCode.NotFound, "ProductNotFound")]
    [InlineData(PersistenceRuleCode.DuplicateCategoryName,
        HttpStatusCode.Conflict, "DuplicateCategoryName")]
    [InlineData(PersistenceRuleCode.DuplicateSku,
        HttpStatusCode.Conflict, "DuplicateSku")]
    [InlineData(PersistenceRuleCode.CategoryHasActiveProducts,
        HttpStatusCode.Conflict, "CategoryHasActiveProducts")]
    [InlineData(PersistenceRuleCode.ConcurrentProductChange,
        HttpStatusCode.Conflict, "ConcurrentProductChange")]
    [InlineData((PersistenceRuleCode)999,
        HttpStatusCode.InternalServerError, "InternalServerError")]
    public async Task HandleAsync_WithPersistenceException_ShouldReturnProblemDetails(
        PersistenceRuleCode code,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        var exception = new PersistenceRuleException(
            code, InternalMessage);
        using var problem = await GetProblemAsync(
            exception, expectedStatus, expectedCode);
        Assert.False(problem.RootElement.TryGetProperty(
            "errors", out _));
    }

    [Theory]
    [InlineData(DomainRuleCode.InactiveCategory,
        HttpStatusCode.NotFound, "InactiveCategory")]
    [InlineData(DomainRuleCode.InactiveProduct,
        HttpStatusCode.NotFound, "InactiveProduct")]
    [InlineData(DomainRuleCode.InsufficientStock,
        HttpStatusCode.Conflict, "InsufficientStock")]
    [InlineData(DomainRuleCode.StockLimitExceeded,
        HttpStatusCode.Conflict, "StockLimitExceeded")]
    [InlineData(DomainRuleCode.ProductHasStock,
        HttpStatusCode.Conflict, "ProductHasStock")]
    [InlineData((DomainRuleCode)999,
        HttpStatusCode.InternalServerError, "InternalServerError")]
    public async Task HandleAsync_WithDomainException_ShouldReturnProblemDetails(
        DomainRuleCode code,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        var exception = new DomainRuleException(
            code, InternalMessage);
        using var problem = await GetProblemAsync(
            exception, expectedStatus, expectedCode);
        Assert.False(problem.RootElement.TryGetProperty(
            "errors", out _));
    }

    [Fact]
    public async Task HandleAsync_WithValidationException_ShouldReturnGroupedErrors()
    {
        var exception = new ValidationException(
            new ValidationFailure[]
            {
                new("Name", "Name is required."),
                new("Name", "Name is invalid."),
                new("CategoryId", "Category identifier must be greater than zero.")
            });
        using var problem = await GetProblemAsync(
            exception,
            HttpStatusCode.BadRequest,
            "ValidationFailed");
        var errors = problem.RootElement.GetProperty("errors");
        var nameErrors = errors.GetProperty("Name");
        var categoryErrors = errors.GetProperty("CategoryId");
        Assert.Equal(2, nameErrors.GetArrayLength());
        Assert.Equal("Name is required.",
            nameErrors[0].GetString());
        Assert.Equal("Name is invalid.",
            nameErrors[1].GetString());
        Assert.Equal(1, categoryErrors.GetArrayLength());
        Assert.Equal("Category identifier must be greater than zero.",
            categoryErrors[0].GetString());
    }

    [Fact]
    public async Task HandleAsync_WithUnexpectedException_ShouldReturnSafeInternalError()
    {
        var exception = new InvalidOperationException(
            InternalMessage);
        using var problem = await GetProblemAsync(
            exception,
            HttpStatusCode.InternalServerError,
            "InternalServerError");
        Assert.Equal("An unexpected error occurred.",
            problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task HandleAsync_WithUnexpectedArgumentException_ShouldReturnInternalError()
    {
        var exception = new ArgumentException(
            InternalMessage);
        using var problem = await GetProblemAsync(
            exception,
            HttpStatusCode.InternalServerError,
            "InternalServerError");
        Assert.Equal("An unexpected error occurred.",
            problem.RootElement.GetProperty("detail").GetString());
    }

    private static async Task<JsonDocument> GetProblemAsync(
        Exception exception,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
        builder.WebHost.UseTestServer();
        builder.Services.AddInventoryErrorHandling();
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.Run(_ => Task.FromException(exception));
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/failure");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain(InternalMessage, body);
        var document = JsonDocument.Parse(body);
        AssertProblemDetails(document.RootElement,
            expectedStatus, expectedCode);

        return document;
    }

    private static void AssertProblemDetails(
        JsonElement problem,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal("about:blank",
            problem.GetProperty("type").GetString());
        Assert.Equal((int)expectedStatus,
            problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode,
            problem.GetProperty("code").GetString());
        Assert.Equal("/failure",
            problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            problem.GetProperty("traceId").GetString()));
        Assert.False(problem.TryGetProperty(
            "stackTrace", out _));
        Assert.False(problem.TryGetProperty(
            "exception", out _));
    }
}
