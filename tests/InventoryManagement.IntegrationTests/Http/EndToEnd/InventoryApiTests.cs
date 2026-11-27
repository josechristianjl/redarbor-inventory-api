using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Dapper;
using InventoryManagement.Api.Contracts.Categories;
using InventoryManagement.Api.Contracts.Common;
using InventoryManagement.Api.Contracts.InventoryMovements;
using InventoryManagement.Api.Contracts.Products;
using InventoryManagement.Application.Models;
using InventoryManagement.Infrastructure.Persistence.Connections;
using InventoryManagement.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static InventoryManagement.IntegrationTests.Http.HttpResponseAssertions;

namespace InventoryManagement.IntegrationTests.Http.EndToEnd;

[Collection("SqlServer integration")]
public class InventoryApiTests : IClassFixture<ReadRepositoryFixture>
{
    private const string CategoriesPath = "/api/v1/categories";
    private const string ProductsPath = "/api/v1/products";
    private const string MovementsPath = "/api/v1/inventory-movements";
    private readonly ReadRepositoryFixture _database;

    public InventoryApiTests(ReadRepositoryFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task InventoryWorkflow_ShouldPersistConsistentState()
    {
        using var rsa = RSA.Create(2048);
        await using var factory = new InventoryApiFactory(_database, rsa);
        using var client = factory.CreateAuthenticatedClient(rsa);
        var suffix = Guid.NewGuid().ToString("N");
        var categoryName = $"Electronics-{suffix}";
        var sku = $"LAPTOP-{suffix}";
        var categoryId = await CreateCategoryAsync(client, categoryName);
        var targetCategoryId = await CreateCategoryAsync(
            client, $"Accessories-{suffix}");
        await VerifyCategoryOperationsAsync(
            client, categoryId, categoryName);
        var productId = await CreateProductAsync(
            client, sku, categoryId);
        await VerifyProductOperationsAsync(
            client, productId, sku, categoryId);
        await VerifyMovementsAsync(client, productId);
        await VerifyProductUpdateAsync(
            client, productId, sku, targetCategoryId);
        await VerifyDeactivationAsync(
            client, productId, categoryId, targetCategoryId);
        await VerifyDatabaseAsync(
            factory.Services, productId, categoryId, targetCategoryId);
    }

    [Theory]
    [InlineData("GET", CategoriesPath)]
    [InlineData("GET", CategoriesPath + "/1")]
    [InlineData("POST", CategoriesPath)]
    [InlineData("PUT", CategoriesPath + "/2147483647")]
    [InlineData("DELETE", CategoriesPath + "/2147483647")]
    [InlineData("GET", ProductsPath)]
    [InlineData("GET", ProductsPath + "/1")]
    [InlineData("POST", ProductsPath)]
    [InlineData("PUT", ProductsPath + "/2147483647")]
    [InlineData("DELETE", ProductsPath + "/2147483647")]
    [InlineData("POST", MovementsPath)]
    public async Task BusinessEndpoint_ShouldEnforceAuthenticationAndScope(
        string method,
        string path)
    {
        using var rsa = RSA.Create(2048);
        await using var factory = new InventoryApiFactory(_database, rsa);
        using var anonymous = factory.CreateHttpsClient();
        using var invalid = factory.CreateHttpsClient();
        invalid.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-jwt");
        using var forbidden = factory.CreateAuthenticatedClient(
            rsa, "other");
        await AssertRejectedAsync(
            anonymous, method, path, HttpStatusCode.Unauthorized);
        await AssertRejectedAsync(
            invalid, method, path, HttpStatusCode.Unauthorized);
        await AssertRejectedAsync(
            forbidden, method, path, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Swagger_InProduction_ShouldNotBeAvailable()
    {
        using var rsa = RSA.Create(2048);
        await using var factory = new InventoryApiFactory(_database, rsa);
        using var client = factory.CreateHttpsClient();
        var environment = factory.Services
            .GetRequiredService<IHostEnvironment>();
        Assert.Equal(
            Environments.Production, environment.EnvironmentName);
        using var response = await client.GetAsync(
            "/swagger/index.html");
        await AssertProblemAsync(
            response, HttpStatusCode.NotFound, "NotFound");
        using var documentResponse = await client.GetAsync(
            "/swagger/v1/swagger.json");
        await AssertProblemAsync(
            documentResponse, HttpStatusCode.NotFound, "NotFound");
    }

    private static async Task<int> CreateCategoryAsync(
        HttpClient client,
        string name)
    {
        var request = new CategoryWriteRequest
        {
            Name = name,
            Description = "Initial description"
        };

        return await CreateResourceAsync(
            client, CategoriesPath, request);
    }

    private static async Task<int> CreateProductAsync(
        HttpClient client,
        string sku,
        int categoryId)
    {
        var request = new ProductWriteRequest
        {
            Sku = sku,
            Name = "Laptop",
            CategoryId = categoryId,
            Description = "Portable computer"
        };

        return await CreateResourceAsync(
            client, ProductsPath, request);
    }

    private static async Task<int> CreateResourceAsync<T>(
        HttpClient client,
        string path,
        T request,
        bool expectLocation = true)
    {
        using var response = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content
            .ReadFromJsonAsync<CreatedResourceResponse>();
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        if (expectLocation)
        {
            Assert.NotNull(response.Headers.Location);
            Assert.EndsWith(
                $"{path}/{result.Id}",
                response.Headers.Location.ToString());
        }
        else
        {
            Assert.Null(response.Headers.Location);
        }

        return result.Id;
    }

    private static async Task VerifyCategoryOperationsAsync(
        HttpClient client,
        int categoryId,
        string name)
    {
        var path = $"{CategoriesPath}/{categoryId}";
        var initial = await GetAsync<CategoryReadModel>(client, path);
        Assert.Equal(name, initial.Name);
        Assert.True(initial.IsActive);
        var request = new CategoryWriteRequest
        {
            Name = name,
            Description = "Updated description"
        };
        using var updated = await client.PutAsJsonAsync(path, request);
        await AssertNoContentAsync(updated);
        var category = await GetAsync<CategoryReadModel>(client, path);
        Assert.Equal("Updated description", category.Description);
        var page = await GetAsync<PagedResult<CategoryReadModel>>(
            client, CategoriesPath);
        Assert.Contains(page.Items, item => item.Id == categoryId);
        using var duplicate = await client.PostAsJsonAsync(
            CategoriesPath,
            request with { Name = name.ToUpperInvariant() });
        await AssertProblemAsync(
            duplicate, HttpStatusCode.Conflict, "DuplicateCategoryName");
    }

    private static async Task VerifyProductOperationsAsync(
        HttpClient client,
        int productId,
        string sku,
        int categoryId)
    {
        var product = await GetAsync<ProductReadModel>(
            client, $"{ProductsPath}/{productId}");
        Assert.Equal(sku, product.Sku);
        Assert.Equal(categoryId, product.CategoryId);
        Assert.Equal(0, product.Stock);
        Assert.True(product.IsActive);
        var page = await GetAsync<PagedResult<ProductReadModel>>(
            client, ProductsPath);
        Assert.Contains(page.Items, item => item.Id == productId);
        using var duplicate = await client.PostAsJsonAsync(
            ProductsPath, new ProductWriteRequest
            {
                Sku = sku.ToLowerInvariant(),
                Name = "Duplicate product",
                CategoryId = categoryId
            });
        await AssertProblemAsync(
            duplicate, HttpStatusCode.Conflict, "DuplicateSku");
    }

    private static async Task VerifyMovementsAsync(
        HttpClient client,
        int productId)
    {
        await RegisterMovementAsync(client, productId, "Entry", 10);
        await AssertStockAsync(client, productId, 10);
        await RegisterMovementAsync(client, productId, "Exit", 4);
        await AssertStockAsync(client, productId, 6);
        using var insufficient = await client.PostAsJsonAsync(
            MovementsPath, new RegisterInventoryMovementRequest
            {
                ProductId = productId,
                Type = "Exit",
                Quantity = 7
            });
        await AssertProblemAsync(
            insufficient, HttpStatusCode.Conflict, "InsufficientStock");
        await AssertStockAsync(client, productId, 6);
    }

    private static async Task RegisterMovementAsync(
        HttpClient client,
        int productId,
        string type,
        int quantity)
    {
        var request = new RegisterInventoryMovementRequest
        {
            ProductId = productId,
            Type = type,
            Quantity = quantity
        };
        await CreateResourceAsync(
            client, MovementsPath, request, expectLocation: false);
    }

    private static async Task VerifyProductUpdateAsync(
        HttpClient client,
        int productId,
        string sku,
        int targetCategoryId)
    {
        var path = $"{ProductsPath}/{productId}";
        var request = new ProductWriteRequest
        {
            Sku = sku,
            Name = "Laptop Pro",
            CategoryId = targetCategoryId,
            Description = "Updated portable computer"
        };
        using var response = await client.PutAsJsonAsync(path, request);
        await AssertNoContentAsync(response);
        var product = await GetAsync<ProductReadModel>(client, path);
        Assert.Equal("Laptop Pro", product.Name);
        Assert.Equal(targetCategoryId, product.CategoryId);
        Assert.Equal(6, product.Stock);
        Assert.True(product.IsActive);
    }

    private static async Task VerifyDeactivationAsync(
        HttpClient client,
        int productId,
        int categoryId,
        int targetCategoryId)
    {
        await VerifyDeletionConflictsAsync(
            client, productId, targetCategoryId);
        await RegisterMovementAsync(client, productId, "Exit", 6);
        await AssertStockAsync(client, productId, 0);
        await DeleteResourceAsync(
            client, $"{ProductsPath}/{productId}", "ProductNotFound");
        await VerifyInactiveProductAsync(client, productId);
        await DeleteResourceAsync(
            client, $"{CategoriesPath}/{categoryId}", "CategoryNotFound");
        await DeleteResourceAsync(
            client,
            $"{CategoriesPath}/{targetCategoryId}",
            "CategoryNotFound");
    }

    private static async Task VerifyDeletionConflictsAsync(
        HttpClient client,
        int productId,
        int categoryId)
    {
        using var productResponse = await client.DeleteAsync(
            $"{ProductsPath}/{productId}");
        await AssertProblemAsync(
            productResponse, HttpStatusCode.Conflict, "ProductHasStock");
        using var categoryResponse = await client.DeleteAsync(
            $"{CategoriesPath}/{categoryId}");
        await AssertProblemAsync(
            categoryResponse,
            HttpStatusCode.Conflict,
            "CategoryHasActiveProducts");
    }

    private static async Task VerifyInactiveProductAsync(
        HttpClient client,
        int productId)
    {
        using var response = await client.PostAsJsonAsync(
            MovementsPath, new RegisterInventoryMovementRequest
            {
                ProductId = productId,
                Type = "Entry",
                Quantity = 10
            });
        await AssertProblemAsync(
            response, HttpStatusCode.NotFound, "ProductNotFound");
        var page = await GetAsync<PagedResult<ProductReadModel>>(
            client, ProductsPath);
        Assert.DoesNotContain(page.Items, item => item.Id == productId);
    }

    private static async Task DeleteResourceAsync(
        HttpClient client,
        string path,
        string notFoundCode)
    {
        using var deleted = await client.DeleteAsync(path);
        await AssertNoContentAsync(deleted);
        using var response = await client.GetAsync(path);
        await AssertProblemAsync(
            response, HttpStatusCode.NotFound, notFoundCode);
    }

    private static async Task AssertStockAsync(
        HttpClient client,
        int productId,
        int expectedStock)
    {
        var product = await GetAsync<ProductReadModel>(
            client, $"{ProductsPath}/{productId}");
        Assert.Equal(expectedStock, product.Stock);
    }

    private static async Task<T> GetAsync<T>(
        HttpClient client,
        string path) where T : class
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(result);

        return result;
    }

    private static async Task VerifyDatabaseAsync(
        IServiceProvider services,
        int productId,
        int categoryId,
        int targetCategoryId)
    {
        var factory = services.GetRequiredService<SqlConnectionFactory>();
        await using var connection =
            await factory.OpenAsync(CancellationToken.None);
        Assert.Equal("inventory_app",
            await connection.ExecuteScalarAsync<string>(
                "SELECT SUSER_SNAME();"));
        await VerifyPersistedProductAsync(
            connection, productId, targetCategoryId);
        await VerifyPersistedCategoriesAsync(
            connection, categoryId, targetCategoryId);
        await VerifyPersistedMovementsAsync(connection, productId);
    }

    private static async Task VerifyPersistedProductAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        int productId,
        int categoryId)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM dbo.Products
            WHERE Id = @ProductId
              AND CategoryId = @CategoryId
              AND Name = N'Laptop Pro'
              AND Stock = 0
              AND IsActive = 0;
            """;
        var count = await connection.ExecuteScalarAsync<int>(
            sql, new { ProductId = productId, CategoryId = categoryId });
        Assert.Equal(1, count);
    }

    private static async Task VerifyPersistedCategoriesAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        int categoryId,
        int targetCategoryId)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM dbo.Categories
            WHERE Id IN (@CategoryId, @TargetCategoryId)
              AND IsActive = 0;
            """;
        var count = await connection.ExecuteScalarAsync<int>(
            sql,
            new
            {
                CategoryId = categoryId,
                TargetCategoryId = targetCategoryId
            });
        Assert.Equal(2, count);
    }

    private static async Task VerifyPersistedMovementsAsync(
        Microsoft.Data.SqlClient.SqlConnection connection,
        int productId)
    {
        const string sql = """
            SELECT CASE Type
                WHEN 1 THEN Quantity
                WHEN 2 THEN -Quantity
            END
            FROM dbo.InventoryMovements
            WHERE ProductId = @ProductId
            ORDER BY Id;
            """;
        var movements = await connection.QueryAsync<int>(
            sql, new { ProductId = productId });
        Assert.Collection(movements,
            quantity => Assert.Equal(10, quantity),
            quantity => Assert.Equal(-4, quantity),
            quantity => Assert.Equal(-6, quantity));
        Assert.Equal(0, movements.Sum());
    }

    private static async Task AssertRejectedAsync(
        HttpClient client,
        string method,
        string path,
        HttpStatusCode expectedStatus)
    {
        using var request = CreateRequest(method, path);
        using var response = await client.SendAsync(request);
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

    private static HttpRequestMessage CreateRequest(
        string method,
        string path)
    {
        var request = new HttpRequestMessage(
            new HttpMethod(method), path);
        if (method is "POST" or "PUT")
        {
            request.Content = CreateRequestContent(path);
        }

        return request;
    }

    private static JsonContent CreateRequestContent(string path)
    {
        var suffix = Guid.NewGuid().ToString("N");
        if (path.StartsWith(CategoriesPath, StringComparison.Ordinal))
        {
            return JsonContent.Create(new CategoryWriteRequest
            {
                Name = $"Security-{suffix}"
            });
        }
        if (path.StartsWith(ProductsPath, StringComparison.Ordinal))
        {
            return JsonContent.Create(new ProductWriteRequest
            {
                Sku = $"SEC-{suffix}",
                Name = "Security test product",
                CategoryId = 1
            });
        }

        return JsonContent.Create(new RegisterInventoryMovementRequest
        {
            ProductId = 1,
            Type = "Entry",
            Quantity = 1
        });
    }
}
