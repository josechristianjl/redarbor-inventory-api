using FluentValidation;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Models;
using InventoryManagement.Application.Products.Queries.GetProducts;
using InventoryManagement.Application.Validation;
using Moq;

namespace InventoryManagement.Application.Tests.Products.Queries.GetProducts;

public class GetProductsQueryHandlerTests
{
    private readonly Mock<IProductReadRepository> _repository =
        new(MockBehavior.Strict);
    private readonly GetProductsQueryHandler _handler;

    public GetProductsQueryHandlerTests()
    {
        _handler = new GetProductsQueryHandler(
            _repository.Object, new PageRequestValidator());
    }

    [Fact]
    public async Task HandleAsync_WithDefaultPagination_ShouldReturnProducts()
    {
        var query = new GetProductsQuery(new PageRequest());
        var expectedRequest = new PageRequest(1, 20);
        var items = new[]
        {
            new ProductReadModel(
                1, "SKU-001", "Laptop", "Portable computer",
                1, 7, true)
        };
        var expected = new PagedResult<ProductReadModel>(
            items, 1, 20, 1);
        _repository.Setup(repository => repository.GetPagedAsync(
                expectedRequest, CancellationToken.None))
            .ReturnsAsync(expected);
        var result = await _handler.HandleAsync(
            query, CancellationToken.None);
        Assert.Same(expected, result);
        _repository.Verify(repository => repository.GetPagedAsync(
            expectedRequest, CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 100)]
    [InlineData(int.MaxValue, 20)]
    public async Task HandleAsync_WithValidPagination_ShouldForwardRequest(
        int page,
        int pageSize)
    {
        using var cancellation = new CancellationTokenSource();
        var request = new PageRequest(page, pageSize);
        var query = new GetProductsQuery(request);
        var expected = new PagedResult<ProductReadModel>(
            Array.Empty<ProductReadModel>(), page, pageSize, 0);
        _repository.Setup(repository => repository.GetPagedAsync(
                request, cancellation.Token))
            .ReturnsAsync(expected);
        var result = await _handler.HandleAsync(
            query, cancellation.Token);
        Assert.Same(expected, result);
        _repository.Verify(repository => repository.GetPagedAsync(
            request, cancellation.Token), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 20, "Page")]
    [InlineData(-1, 20, "Page")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, -1, "PageSize")]
    [InlineData(1, 101, "PageSize")]
    public async Task HandleAsync_WithInvalidPagination_ShouldRejectQuery(
        int page,
        int pageSize,
        string propertyName)
    {
        var query = new GetProductsQuery(
            new PageRequest(page, pageSize));
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(query, CancellationToken.None));
        var failure = Assert.Single(exception.Errors);
        Assert.Equal(propertyName, failure.PropertyName);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithMultipleInvalidValues_ShouldReturnAllErrors()
    {
        var query = new GetProductsQuery(new PageRequest(0, 101));
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(query, CancellationToken.None));
        Assert.Equal(2, exception.Errors.Count());
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "Page");
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "PageSize");
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_ShouldNotCallRepository()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var query = new GetProductsQuery(new PageRequest());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(query, cancellation.Token));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_ShouldPropagateException()
    {
        var request = new PageRequest();
        var query = new GetProductsQuery(request);
        var expected = new InvalidOperationException(
            "Unexpected persistence failure.");
        _repository.Setup(repository => repository.GetPagedAsync(
                request, CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(query, CancellationToken.None));
        Assert.Same(expected, exception);
        _repository.Verify(repository => repository.GetPagedAsync(
            request, CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }
}
