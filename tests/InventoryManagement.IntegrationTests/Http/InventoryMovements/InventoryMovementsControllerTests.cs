using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using InventoryManagement.Api.Contracts.Common;
using InventoryManagement.Api.Contracts.InventoryMovements;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.Domain.Exceptions;
using Moq;
using static InventoryManagement.IntegrationTests.Http.HttpResponseAssertions;

namespace InventoryManagement.IntegrationTests.Http.InventoryMovements;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "xUnit v2 invokes IAsyncLifetime.DisposeAsync to release owned resources.")]
public class InventoryMovementsControllerTests : IAsyncLifetime
{
    private const string BasePath = "/api/v1/inventory-movements";
    private readonly ApiControllerTestHost _host = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _host.StartAsync();
        _client = _host.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.DisposeAsync();
    }

    [Theory]
    [InlineData("Entry", MovementType.Entry, 10)]
    [InlineData("Exit", MovementType.Exit, 4)]
    public async Task Register_WithValidRequest_ShouldReturnCreated(
        string type,
        MovementType expectedType,
        int quantity)
    {
        var request = new RegisterInventoryMovementRequest
        {
            ProductId = 1,
            Type = type,
            Quantity = quantity
        };
        _host.MovementWrite.Setup(repository => repository.RegisterAsync(
                It.Is<InventoryMovement>(movement =>
                    movement.Id == 0 &&
                    movement.ProductId == 1 &&
                    movement.Type == expectedType &&
                    movement.Quantity == quantity &&
                    movement.CreatedAt != default &&
                    movement.CreatedAt.Kind == DateTimeKind.Utc),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);
        using var response = await _client.PostAsJsonAsync(
            BasePath, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<CreatedResourceResponse>();
        Assert.NotNull(result);
        Assert.Equal(25, result.Id);
        Assert.Null(response.Headers.Location);
        VerifyMovementRegistration();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("entry")]
    [InlineData("exit")]
    [InlineData("1")]
    [InlineData("Transfer")]
    [InlineData(" Entry ")]
    public async Task Register_WithInvalidMovementType_ShouldReturnBadRequest(
        string? type)
    {
        var request = CreateValidRequest() with { Type = type };
        using var response = await _client.PostAsJsonAsync(
            BasePath, request);
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "ValidationFailed");
        _host.VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("true")]
    public async Task Register_WithNonStringMovementType_ShouldReturnBadRequest(
        string typeJson)
    {
        var json = "{\"productId\":1,\"type\":" +
            typeJson + ",\"quantity\":10}";
        using var content = new StringContent(
            json, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(
            BasePath, content);
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "InvalidRequest");
        _host.VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task Register_WithInvalidData_ShouldReturnBadRequest(
        int productId,
        int quantity)
    {
        var request = CreateValidRequest() with
        {
            ProductId = productId,
            Quantity = quantity
        };
        using var response = await _client.PostAsJsonAsync(
            BasePath, request);
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "ValidationFailed");
        _host.VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData(
        "{\"productId\":1,\"type\":\"Entry\",\"quantity\":10,\"id\":25}")]
    [InlineData(
        "{\"productId\":1,\"type\":\"Entry\",\"quantity\":10,\"createdAt\":\"2026-01-01T00:00:00Z\"}")]
    [InlineData(
        "{\"productId\":1,\"type\":\"Entry\",\"quantity\":10,\"stock\":500}")]
    public async Task Register_WithUnknownProperty_ShouldReturnBadRequest(
        string json)
    {
        using var content = new StringContent(
            json, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(
            BasePath, content);
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "InvalidRequest");
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Register_WithMissingMovementType_ShouldReturnBadRequest()
    {
        var request = new
        {
            productId = 1,
            quantity = 10
        };
        using var response = await _client.PostAsJsonAsync(
            BasePath, request);
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "ValidationFailed");
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Register_WithUnavailableProduct_ShouldReturnNotFound()
    {
        var request = CreateValidRequest() with { ProductId = 99 };
        _host.MovementWrite.Setup(repository => repository.RegisterAsync(
                It.IsAny<InventoryMovement>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PersistenceRuleException(
                PersistenceRuleCode.ProductNotFound,
                "Product not found."));
        using var response = await _client.PostAsJsonAsync(
            BasePath, request);
        await AssertProblemAsync(
            response, HttpStatusCode.NotFound, "ProductNotFound");
        VerifyMovementRegistration();
    }

    [Theory]
    [InlineData(
        "Exit", DomainRuleCode.InsufficientStock, "InsufficientStock")]
    [InlineData(
        "Entry", DomainRuleCode.StockLimitExceeded, "StockLimitExceeded")]
    public async Task Register_WithDomainRuleViolation_ShouldReturnConflict(
        string type,
        DomainRuleCode rule,
        string code)
    {
        var request = CreateValidRequest() with { Type = type };
        _host.MovementWrite.Setup(repository => repository.RegisterAsync(
                It.IsAny<InventoryMovement>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainRuleException(
                rule, "Domain rule violation."));
        using var response = await _client.PostAsJsonAsync(
            BasePath, request);
        await AssertProblemAsync(
            response, HttpStatusCode.Conflict, code);
        VerifyMovementRegistration();
    }

    [Fact]
    public async Task Register_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        using var anonymousClient = _host.CreateClient(
            authenticated: false);
        using var response = await anonymousClient.PostAsJsonAsync(
            BasePath, CreateValidRequest());
        await AssertProblemAsync(
            response, HttpStatusCode.Unauthorized, "Unauthorized");
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Register_WhenRepositoryFails_ShouldReturnSafeInternalError()
    {
        _host.MovementWrite.Setup(repository => repository.RegisterAsync(
                It.IsAny<InventoryMovement>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "INTERNAL_TEST_MARKER"));
        using var response = await _client.PostAsJsonAsync(
            BasePath, CreateValidRequest());
        await AssertProblemAsync(
            response,
            HttpStatusCode.InternalServerError,
            "InternalServerError");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("INTERNAL_TEST_MARKER", body);
        VerifyMovementRegistration();
    }

    private static RegisterInventoryMovementRequest CreateValidRequest()
    {
        return new RegisterInventoryMovementRequest
        {
            ProductId = 1,
            Type = "Entry",
            Quantity = 10
        };
    }

    private void VerifyMovementRegistration()
    {
        _host.MovementWrite.Verify(repository => repository.RegisterAsync(
            It.IsAny<InventoryMovement>(),
            It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }
}
