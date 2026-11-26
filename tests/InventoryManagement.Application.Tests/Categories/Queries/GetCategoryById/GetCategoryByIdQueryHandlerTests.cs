using FluentValidation;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Categories.Queries.GetCategoryById;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Application.Models;
using Moq;

namespace InventoryManagement.Application.Tests.Categories.Queries.GetCategoryById;

public class GetCategoryByIdQueryHandlerTests
{
    private readonly Mock<ICategoryReadRepository> _repository =
        new(MockBehavior.Strict);
    private readonly GetCategoryByIdQueryHandler _handler;

    public GetCategoryByIdQueryHandlerTests()
    {
        _handler = new GetCategoryByIdQueryHandler(
            _repository.Object,
            new GetCategoryByIdQueryValidator());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public async Task HandleAsync_WithValidId_ShouldReturnCategory(int id)
    {
        using var cancellation = new CancellationTokenSource();
        var query = new GetCategoryByIdQuery(id);
        var expected = new CategoryReadModel(
            id, "Electronics", "Electronic devices", true);
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
    public async Task HandleAsync_WithUnavailableCategory_ShouldThrowNotFound()
    {
        var query = new GetCategoryByIdQuery(999);
        _repository.Setup(repository => repository.GetByIdAsync(
                query.Id, CancellationToken.None))
            .ReturnsAsync((CategoryReadModel?)null);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            _handler.HandleAsync(query, CancellationToken.None));
        Assert.Equal(PersistenceRuleCode.CategoryNotFound, exception.Code);
        _repository.Verify(repository => repository.GetByIdAsync(
            query.Id, CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_WithInvalidId_ShouldRejectQuery(int id)
    {
        var query = new GetCategoryByIdQuery(id);
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
        var query = new GetCategoryByIdQuery(1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(query, cancellation.Token));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_ShouldPropagateException()
    {
        var query = new GetCategoryByIdQuery(1);
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
