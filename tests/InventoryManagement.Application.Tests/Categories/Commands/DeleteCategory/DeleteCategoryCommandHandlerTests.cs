using FluentValidation;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Categories.Commands.DeleteCategory;
using InventoryManagement.Application.Exceptions;
using Moq;

namespace InventoryManagement.Application.Tests.Categories.Commands.DeleteCategory;

public class DeleteCategoryCommandHandlerTests
{
    private readonly Mock<ICategoryWriteRepository> _repository =
        new(MockBehavior.Strict);
    private readonly DeleteCategoryCommandHandler _handler;

    public DeleteCategoryCommandHandlerTests()
    {
        _handler = new DeleteCategoryCommandHandler(
            _repository.Object,
            new DeleteCategoryCommandValidator());
    }

    [Fact]
    public async Task HandleAsync_WithValidId_ShouldDeactivateCategory()
    {
        using var cancellation = new CancellationTokenSource();
        var command = new DeleteCategoryCommand(1);
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
        var command = new DeleteCategoryCommand(id);
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        var failure = Assert.Single(exception.Errors);
        Assert.Equal("Id", failure.PropertyName);
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(PersistenceRuleCode.CategoryNotFound)]
    [InlineData(PersistenceRuleCode.CategoryHasActiveProducts)]
    public async Task HandleAsync_WithPersistenceRuleViolation_ShouldPropagateException(
        PersistenceRuleCode code)
    {
        var command = new DeleteCategoryCommand(1);
        var expected = new PersistenceRuleException(
            code, "Persistence rule violation.");
        _repository.Setup(repository => repository.DeactivateAsync(
                command.Id, CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        Assert.Equal(code, exception.Code);
        _repository.Verify(repository => repository.DeactivateAsync(
            command.Id, CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_ShouldNotCallRepository()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var command = new DeleteCategoryCommand(1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(command, cancellation.Token));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_ShouldPropagateException()
    {
        var command = new DeleteCategoryCommand(1);
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
