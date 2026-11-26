using FluentValidation;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Application.Products.Commands.DeleteProduct;
using InventoryManagement.Domain.Exceptions;
using Moq;

namespace InventoryManagement.Application.Tests.Products.Commands.DeleteProduct;

public class DeleteProductCommandHandlerTests
{
    private readonly Mock<IProductWriteRepository> _repository =
        new(MockBehavior.Strict);
    private readonly DeleteProductCommandHandler _handler;

    public DeleteProductCommandHandlerTests()
    {
        _handler = new DeleteProductCommandHandler(
            _repository.Object,
            new DeleteProductCommandValidator());
    }

    [Fact]
    public async Task HandleAsync_WithValidId_ShouldDeactivateProduct()
    {
        using var cancellation = new CancellationTokenSource();
        var command = new DeleteProductCommand(1);
        _repository.Setup(repository => repository.DeactivateAsync(
                command.Id, cancellation.Token))
            .Returns(Task.CompletedTask);
        await _handler.HandleAsync(command, cancellation.Token);
        _repository.Verify(repository => repository.DeactivateAsync(
            command.Id, cancellation.Token), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_WithInvalidId_ShouldRejectCommand(int id)
    {
        var command = new DeleteProductCommand(id);
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        var failure = Assert.Single(exception.Errors);
        Assert.Equal("Id", failure.PropertyName);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithUnavailableProduct_ShouldPropagateNotFound()
    {
        var command = new DeleteProductCommand(1);
        var expected = new PersistenceRuleException(
            PersistenceRuleCode.ProductNotFound,
            "The active product was not found.");
        _repository.Setup(repository => repository.DeactivateAsync(
                command.Id, CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        Assert.Equal(PersistenceRuleCode.ProductNotFound, exception.Code);
        _repository.Verify(repository => repository.DeactivateAsync(
            command.Id, CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithAvailableStock_ShouldPropagateConflict()
    {
        var command = new DeleteProductCommand(1);
        var expected = new DomainRuleException(
            DomainRuleCode.ProductHasStock,
            "A product with available stock cannot be deactivated.");
        _repository.Setup(repository => repository.DeactivateAsync(
                command.Id, CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        Assert.Equal(DomainRuleCode.ProductHasStock, exception.Code);
        _repository.Verify(repository => repository.DeactivateAsync(
            command.Id, CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_ShouldNotCallRepository()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var command = new DeleteProductCommand(1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(command, cancellation.Token));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_ShouldPropagateException()
    {
        var command = new DeleteProductCommand(1);
        var expected = new InvalidOperationException(
            "Unexpected persistence failure.");
        _repository.Setup(repository => repository.DeactivateAsync(
                command.Id, CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        _repository.Verify(repository => repository.DeactivateAsync(
            command.Id, CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }
}
