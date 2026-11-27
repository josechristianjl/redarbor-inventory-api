using System.Net;
using System.Net.Http.Json;
using InventoryManagement.Api.Contracts.Categories;
using InventoryManagement.Api.Contracts.Common;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Application.Models;
using InventoryManagement.Domain.Entities;
using Moq;
using System.Diagnostics.CodeAnalysis;
using static InventoryManagement.IntegrationTests.Http.HttpResponseAssertions;
using InventoryManagement.Domain.Parameters;

namespace InventoryManagement.IntegrationTests.Http.Categories;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "xUnit v2 invokes IAsyncLifetime.DisposeAsync to release owned resources.")]
public class CategoriesControllerTests : IAsyncLifetime
{
    private const string BasePath = "/api/v1/categories";
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
    public async Task GetAll_WithValidPagination_ShouldReturnCategories(
        string query,
        int page,
        int pageSize)
    {
        var category = new CategoryReadModel(
            1, "Electronics", "Electronic devices", true);
        var pagination = new PageRequest(page, pageSize);
        var expected = new PagedResult<CategoryReadModel>(
            [category], page, pageSize, 15);
        _host.CategoryRead.Setup(repository => repository.GetPagedAsync(
                pagination, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        using var response = await _client.GetAsync(BasePath + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<PagedResult<CategoryReadModel>>();
        Assert.NotNull(result);
        Assert.Equal(page, result.Page);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(category, Assert.Single(result.Items));
        _host.CategoryRead.Verify(repository => repository.GetPagedAsync(
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
    public async Task GetById_WithExistingCategory_ShouldReturnCategory()
    {
        var expected = new CategoryReadModel(
            1, "Electronics", null, true);
        _host.CategoryRead.Setup(repository => repository.GetByIdAsync(
                1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        using var response = await _client.GetAsync($"{BasePath}/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<CategoryReadModel>();
        Assert.Equal(expected, result);
        _host.CategoryRead.Verify(repository => repository.GetByIdAsync(
            1, It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task GetById_WithUnavailableCategory_ShouldReturnNotFound()
    {
        _host.CategoryRead.Setup(repository => repository.GetByIdAsync(
                99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CategoryReadModel?)null);
        using var response = await _client.GetAsync($"{BasePath}/99");
        await AssertProblemAsync(
            response, HttpStatusCode.NotFound, "CategoryNotFound");
        _host.CategoryRead.Verify(repository => repository.GetByIdAsync(
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
        var request = new CategoryWriteRequest
        {
            Name = "  Electronics  ",
            Description = "  Electronic devices  "
        };
        _host.CategoryWrite.Setup(repository => repository.CreateAsync(
                It.Is<Category>(category =>
                    category.Id == 0 &&
                    category.Name == "Electronics" &&
                    category.Description == "Electronic devices" &&
                    category.IsActive),
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
        _host.CategoryWrite.Verify(repository => repository.CreateAsync(
            It.IsAny<Category>(),
            It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Create_WithInvalidName_ShouldReturnBadRequest(
        string? name)
    {
        var request = new CategoryWriteRequest { Name = name };
        using var response = await _client.PostAsJsonAsync(
            BasePath, request);
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "ValidationFailed");
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Create_WithDuplicateName_ShouldReturnConflict()
    {
        var request = new CategoryWriteRequest
        {
            Name = "Electronics"
        };
        _host.CategoryWrite.Setup(repository => repository.CreateAsync(
                It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PersistenceRuleException(
                PersistenceRuleCode.DuplicateCategoryName,
                "Duplicate category."));
        using var response = await _client.PostAsJsonAsync(
            BasePath, request);
        await AssertProblemAsync(
            response, HttpStatusCode.Conflict, "DuplicateCategoryName");
        _host.CategoryWrite.Verify(repository => repository.CreateAsync(
            It.IsAny<Category>(),
            It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Create_WithUnknownProperty_ShouldReturnBadRequest()
    {
        var request = new
        {
            name = "Electronics",
            isActive = false
        };
        using var response = await _client.PostAsJsonAsync(
            BasePath, request);
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "InvalidRequest");
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Update_WithValidRequest_ShouldReturnNoContent()
    {
        var request = new CategoryWriteRequest
        {
            Name = "  Electronics  ",
            Description = "  Updated description  "
        };
        _host.CategoryWrite.Setup(repository => repository.UpdateAsync(
                7, new CategoryDetails(request.Name!, request.Description),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        using var response = await _client.PutAsJsonAsync(
            $"{BasePath}/7", request);
        await AssertNoContentAsync(response);
        _host.CategoryWrite.Verify(repository => repository.UpdateAsync(
            7, new CategoryDetails(request.Name!, request.Description),
            It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Update_WithUnavailableCategory_ShouldReturnNotFound()
    {
        var request = new CategoryWriteRequest
        {
            Name = "Electronics"
        };
        _host.CategoryWrite.Setup(repository => repository.UpdateAsync(
                99, new CategoryDetails(request.Name!, request.Description),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PersistenceRuleException(
                PersistenceRuleCode.CategoryNotFound,
                "Category not found."));
        using var response = await _client.PutAsJsonAsync(
            $"{BasePath}/99", request);
        await AssertProblemAsync(
            response, HttpStatusCode.NotFound, "CategoryNotFound");
        _host.CategoryWrite.Verify(repository => repository.UpdateAsync(
            99, new CategoryDetails(request.Name!, request.Description),
            It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Update_WithInvalidIdentifier_ShouldReturnBadRequest()
    {
        var request = new CategoryWriteRequest
        {
            Name = "Electronics"
        };
        using var response = await _client.PutAsJsonAsync(
            $"{BasePath}/0", request);
        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "ValidationFailed");
        _host.VerifyNoRepositoryCalls();
    }

    [Fact]
    public async Task Delete_WithAvailableCategory_ShouldReturnNoContent()
    {
        _host.CategoryWrite.Setup(repository => repository.DeactivateAsync(
                1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        using var response = await _client.DeleteAsync($"{BasePath}/1");
        await AssertNoContentAsync(response);
        _host.CategoryWrite.Verify(repository => repository.DeactivateAsync(
            1, It.IsAny<CancellationToken>()), Times.Once());
        _host.VerifyNoRepositoryCalls();
    }

    [Theory]
    [InlineData(PersistenceRuleCode.CategoryHasActiveProducts,
        HttpStatusCode.Conflict, "CategoryHasActiveProducts")]
    [InlineData(PersistenceRuleCode.CategoryNotFound,
        HttpStatusCode.NotFound, "CategoryNotFound")]
    public async Task Delete_WithPersistenceRuleViolation_ShouldReturnProblem(
        PersistenceRuleCode rule,
        HttpStatusCode status,
        string code)
    {
        _host.CategoryWrite.Setup(repository => repository.DeactivateAsync(
                1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PersistenceRuleException(
                rule, "Persistence rule violation."));
        using var response = await _client.DeleteAsync($"{BasePath}/1");
        await AssertProblemAsync(response, status, code);
        _host.CategoryWrite.Verify(repository => repository.DeactivateAsync(
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
}
