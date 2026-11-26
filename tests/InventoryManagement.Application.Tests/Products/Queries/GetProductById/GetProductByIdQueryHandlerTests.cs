using FluentValidation;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Application.Models;
using InventoryManagement.Application.Products.Queries.GetProductById;
using Moq;

namespace InventoryManagement.Application.Tests.Products.Queries.GetProductById;

public class GetProductByIdQueryHandlerTests
{
    private readonly Mock<IProductReadRepository> _repository =
        new(MockBehavior.Strict);
    private readonly GetProductByIdQueryHandler _handler;

    public GetProductByIdQueryHandlerTests()
    {
        _handler = new GetProductByIdQueryHandler(
            _repository.Object,
            new GetProductByIdQueryValidator());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public async Task HandleAsync_WithValidId_ShouldReturnProduct(int id)
    {
        using var cancellation = new CancellationTokenSource();
        var query = new GetProductByIdQuery(id);
        var expected = new ProductReadModel(
            id, "SKU-001", "Laptop", "Portable computer",
            1, 7, true);
        _repository.Setup(repository => repository.GetByIdAsync(
                id, cancellation.Token))
            .ReturnsAsync(expected);
        var result = await _handler.HandleAsync(
            query, cancellation.Token);
        Assert.Same(expected, result);
        _repository.Verify(repository => repository.GetByIdAsync(
            id, cancellation.Token), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithUnavailableProduct_ShouldThrowNotFound()
    {
        var query = new GetProductByIdQuery(999);
        _repository.Setup(repository => repository.GetByIdAsync(
                query.Id, CancellationToken.None))
            .ReturnsAsync((ProductReadModel?)null);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            _handler.HandleAsync(query, CancellationToken.None));
        Assert.Equal(PersistenceRuleCode.ProductNotFound, exception.Code);
        _repository.Verify(repository => repository.GetByIdAsync(
            query.Id, CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_WithInvalidId_ShouldRejectQuery(int id)
    {
        var query = new GetProductByIdQuery(id);
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(query, CancellationToken.None));
        var failure = Assert.Single(exception.Errors);
        Assert.Equal("Id", failure.PropertyName);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_ShouldNotCallRepository()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var query = new GetProductByIdQuery(1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(query, cancellation.Token));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_ShouldPropagateException()
    {
        var query = new GetProductByIdQuery(1);
        var expected = new InvalidOperationException(
            "Unexpected persistence failure.");
        _repository.Setup(repository => repository.GetByIdAsync(
                query.Id, CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(query, CancellationToken.None));
        Assert.Same(expected, exception);
        _repository.Verify(repository => repository.GetByIdAsync(
            query.Id, CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }
}
