using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using InventoryManagement.Api.Contracts.Common;
using InventoryManagement.Api.Contracts.Products;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Application.Models;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Exceptions;
using InventoryManagement.Domain.Parameters;
using Moq;
using static InventoryManagement.IntegrationTests.Http.HttpResponseAssertions;

namespace InventoryManagement.IntegrationTests.Http.Products;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "xUnit v2 invokes IAsyncLifetime.DisposeAsync to release owned resources.")]
public class ProductsControllerTests : IAsyncLifetime
{
    private const string BasePath = "/api/v1/products";
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
    [InlineData("", 1, 20)]
    [InlineData("?page=2&pageSize=10", 2, 10)]
    public async Task GetAll_WithValidPagination_ShouldReturnProducts(
        string query,
        int page,
        int pageSize)
    {
        var product = new ProductReadModel(
            1, "SKU-001", "Laptop", "Portable computer", 1, 7, true);
        var pagination = new PageRequest(page, pageSize);
        var expected = new PagedResult<ProductReadModel>(
            [product], page, pageSize, 15);
        _host.ProductRead.Setup(repository => repository.GetPagedAsync(
                pagination, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        using var response = await _client.GetAsync(BasePath + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<PagedResult<ProductReadModel>>();
        Assert.NotNull(result);
        Assert.Equal(page, result.Page);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(product, Assert.Single(result.Items));
        _host.ProductRead.Verify(repository => repository.GetPagedAsync(
            pagination, It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData("?page=0", "ValidationFailed")]
    [InlineData("?pageSize=101", "ValidationFailed")]
    [InlineData("?page=invalid", "InvalidRequest")]
    public async Task GetAll_WithInvalidPagination_ShouldReturnBadRequest(
        string query,
        string code)
    {
        using var response = await _client.GetAsync(BasePath + query);
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, code);
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task GetById_WithExistingProduct_ShouldReturnProduct()
    {
        var expected = new ProductReadModel(
            1, "SKU-001", "Laptop", null, 1, 7, true);
        _host.ProductRead.Setup(repository => repository.GetByIdAsync(
                1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        using var response = await _client.GetAsync($"{BasePath}/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<ProductReadModel>();
        Assert.Equal(expected, result);
        _host.ProductRead.Verify(repository => repository.GetByIdAsync(
            1, It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task GetById_WithUnavailableProduct_ShouldReturnNotFound()
    {
        _host.ProductRead.Setup(repository => repository.GetByIdAsync(
                99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductReadModel?)null);
        using var response = await _client.GetAsync($"{BasePath}/99");
        await AssertProblemAsync(
            response, HttpStatusCode.NotFound, "ProductNotFound");
        _host.ProductRead.Verify(repository => repository.GetByIdAsync(
            99, It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData("0", "ValidationFailed")]
    [InlineData("invalid", "InvalidRequest")]
    public async Task GetById_WithInvalidIdentifier_ShouldReturnBadRequest(
        string id,
        string code)
    {
        using var response = await _client.GetAsync($"{BasePath}/{id}");
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, code);
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Create_WithValidRequest_ShouldReturnCreated()
    {
        var request = new ProductWriteRequest
        {
            Sku = "  SKU-001  ",
            Name = "  Laptop  ",
            CategoryId = 1,
            Description = "  Portable computer  "
        };
        _host.ProductWrite.Setup(repository => repository.CreateAsync(
                It.Is<Product>(product =>
                    product.Id == 0 &&
                    product.SKU == "SKU-001" &&
                    product.Name == "Laptop" &&
                    product.CategoryId == 1 &&
                    product.Description == "Portable computer" &&
                    product.Stock == 0 &&
                    product.IsActive),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);
        using var response = await _client.PostAsJsonAsync(
            BasePath, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<CreatedResourceResponse>();
        Assert.NotNull(result);
        Assert.Equal(25, result.Id);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith(
            $"{BasePath}/25", response.Headers.Location.ToString());
        _host.ProductWrite.Verify(repository => repository.CreateAsync(
            It.IsAny<Product>(),
            It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData(null, "Laptop", 1)]
    [InlineData("SKU-001", null, 1)]
    [InlineData("SKU-001", "Laptop", 0)]
    public async Task Create_WithInvalidData_ShouldReturnBadRequest(
        string? sku,
        string? name,
        int categoryId)
    {
        var request = new ProductWriteRequest
        {
            Sku = sku,
            Name = name,
            CategoryId = categoryId
        };
        using var response = await _client.PostAsJsonAsync(
            BasePath, request);
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "ValidationFailed");
        _host.VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData(
        "{\"sku\":\"SKU-001\",\"name\":\"Laptop\",\"categoryId\":1,\"stock\":500}")]
    [InlineData(
        "{\"sku\":\"SKU-001\",\"name\":\"Laptop\",\"categoryId\":1,\"id\":25}")]
    public async Task Create_WithUnknownProperty_ShouldReturnBadRequest(
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

    [Theory]
    [InlineData(
        PersistenceRuleCode.DuplicateSku,
        HttpStatusCode.Conflict, "DuplicateSku")]
    [InlineData(
        PersistenceRuleCode.CategoryNotFound,
        HttpStatusCode.NotFound, "CategoryNotFound")]
    public async Task Create_WithPersistenceRuleViolation_ShouldReturnProblem(
        PersistenceRuleCode rule,
        HttpStatusCode status,
        string code)
    {
        var request = CreateValidRequest();
        _host.ProductWrite.Setup(repository => repository.CreateAsync(
                It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PersistenceRuleException(
                rule, "Persistence rule violation."));
        using var response = await _client.PostAsJsonAsync(
            BasePath, request);
        await AssertProblemAsync(response, status, code);
        _host.ProductWrite.Verify(repository => repository.CreateAsync(
            It.IsAny<Product>(),
            It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Update_WithValidRequest_ShouldReturnNoContent()
    {
        var request = new ProductWriteRequest
        {
            Sku = "  SKU-002  ",
            Name = "  Monitor  ",
            CategoryId = 2,
            Description = "  External display  "
        };
        var details = CreateExpectedDetails(request);
        _host.ProductWrite.Setup(repository => repository.UpdateAsync(
                7, details, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        using var response = await _client.PutAsJsonAsync(
            $"{BasePath}/7", request);
        await AssertNoContentAsync(response);
        _host.ProductWrite.Verify(repository => repository.UpdateAsync(
            7, details, It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Update_WithInvalidIdentifier_ShouldReturnBadRequest()
    {
        using var response = await _client.PutAsJsonAsync(
            $"{BasePath}/0", CreateValidRequest());
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "ValidationFailed");
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Update_WithInvalidData_ShouldReturnBadRequest()
    {
        var request = CreateValidRequest() with { Sku = " " };
        using var response = await _client.PutAsJsonAsync(
            $"{BasePath}/1", request);
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "ValidationFailed");
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Update_WithUnknownProperty_ShouldReturnBadRequest()
    {
        var request = new
        {
            sku = "SKU-001",
            name = "Laptop",
            categoryId = 1,
            isActive = false
        };
        using var response = await _client.PutAsJsonAsync(
            $"{BasePath}/1", request);
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "InvalidRequest");
        _host.VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData(
        PersistenceRuleCode.ProductNotFound,
        HttpStatusCode.NotFound, "ProductNotFound")]
    [InlineData(
        PersistenceRuleCode.CategoryNotFound,
        HttpStatusCode.NotFound, "CategoryNotFound")]
    [InlineData(
        PersistenceRuleCode.DuplicateSku,
        HttpStatusCode.Conflict, "DuplicateSku")]
    [InlineData(
        PersistenceRuleCode.ConcurrentProductChange,
        HttpStatusCode.Conflict, "ConcurrentProductChange")]
    public async Task Update_WithPersistenceRuleViolation_ShouldReturnProblem(
        PersistenceRuleCode rule,
        HttpStatusCode status,
        string code)
    {
        var request = CreateValidRequest();
        var details = CreateExpectedDetails(request);
        _host.ProductWrite.Setup(repository => repository.UpdateAsync(
                7, details, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PersistenceRuleException(
                rule, "Persistence rule violation."));
        using var response = await _client.PutAsJsonAsync(
            $"{BasePath}/7", request);
        await AssertProblemAsync(response, status, code);
        _host.ProductWrite.Verify(repository => repository.UpdateAsync(
            7, details, It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Delete_WithAvailableProduct_ShouldReturnNoContent()
    {
        _host.ProductWrite.Setup(repository => repository.DeactivateAsync(
                1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        using var response = await _client.DeleteAsync($"{BasePath}/1");
        await AssertNoContentAsync(response);
        _host.ProductWrite.Verify(repository => repository.DeactivateAsync(
            1, It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Delete_WithInvalidIdentifier_ShouldReturnBadRequest()
    {
        using var response = await _client.DeleteAsync($"{BasePath}/0");
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "ValidationFailed");
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Delete_WithUnavailableProduct_ShouldReturnNotFound()
    {
        _host.ProductWrite.Setup(repository => repository.DeactivateAsync(
                99, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PersistenceRuleException(
                PersistenceRuleCode.ProductNotFound,
                "Product not found."));
        using var response = await _client.DeleteAsync($"{BasePath}/99");
        await AssertProblemAsync(
            response, HttpStatusCode.NotFound, "ProductNotFound");
        _host.ProductWrite.Verify(repository => repository.DeactivateAsync(
            99, It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Delete_WithAvailableStock_ShouldReturnConflict()
    {
        _host.ProductWrite.Setup(repository => repository.DeactivateAsync(
                1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainRuleException(
                DomainRuleCode.ProductHasStock,
                "The product has available stock."));
        using var response = await _client.DeleteAsync($"{BasePath}/1");
        await AssertProblemAsync(
            response, HttpStatusCode.Conflict, "ProductHasStock");
        _host.ProductWrite.Verify(repository => repository.DeactivateAsync(
            1, It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/1")]
    [InlineData("POST", "")]
    [InlineData("PUT", "/1")]
    [InlineData("DELETE", "/1")]
    public async Task Request_WithoutAuthentication_ShouldReturnUnauthorized(
        string method,
        string suffix)
    {
        using var anonymousClient = _host.CreateClient(
            authenticated: false);
        using var request = new HttpRequestMessage(
            new HttpMethod(method), BasePath + suffix);
        using var response = await anonymousClient.SendAsync(request);
        await AssertProblemAsync(
            response, HttpStatusCode.Unauthorized, "Unauthorized");
        _host.VerifyNoRepositoryCalls();
    }

    private static ProductWriteRequest CreateValidRequest()
    {
        return new ProductWriteRequest
        {
            Sku = "SKU-001",
            Name = "Laptop",
            CategoryId = 1,
            Description = "Portable computer"
        };
    }

    private static ProductDetails CreateExpectedDetails(
        ProductWriteRequest request)
    {
        return new ProductDetails(
            request.Sku!,
            request.Name!,
            request.CategoryId,
            request.Description);
    }
}
