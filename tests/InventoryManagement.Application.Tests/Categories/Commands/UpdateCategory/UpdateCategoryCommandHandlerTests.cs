using FluentValidation;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Categories.Commands.UpdateCategory;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Domain.Entities;
using Moq;

namespace InventoryManagement.Application.Tests.Categories.Commands.UpdateCategory;

public class UpdateCategoryCommandHandlerTests
{
    private readonly Mock<ICategoryWriteRepository> _repository =
        new(MockBehavior.Strict);
    private readonly UpdateCategoryCommandHandler _handler;

    public UpdateCategoryCommandHandlerTests()
    {
        _handler = new UpdateCategoryCommandHandler(
            _repository.Object,
            new UpdateCategoryCommandValidator());
    }

    [Theory]
    [InlineData("  Technology  ", "  Technology products  ")]
    [InlineData("Technology", null)]
    [InlineData("Technology", "   ")]
    public async Task HandleAsync_WithValidData_ShouldUpdateCategory(
        string name,
        string? description)
    {
        var command = new UpdateCategoryCommand(
            1, name, description);
        await AssertSuccessfulUpdateAsync(command);
    }

    [Fact]
    public async Task HandleAsync_WithMaximumLengths_ShouldUpdateCategory()
    {
        var name = new string('A', Category.MaxNameLength);
        var description = new string(
            'B', Category.MaxDescriptionLength);
        var command = new UpdateCategoryCommand(
            1, $"  {name}  ", $"  {description}  ");
        await AssertSuccessfulUpdateAsync(command);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_WithInvalidId_ShouldRejectCommand(
        int id)
    {
        var command = new UpdateCategoryCommand(
            id, "Technology");
        await AssertInvalidCommandAsync(command, "Id");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WithInvalidName_ShouldRejectCommand(
        string? name)
    {
        var command = new UpdateCategoryCommand(1, name!);
        await AssertInvalidCommandAsync(command, "Name");
    }

    [Fact]
    public async Task HandleAsync_WithExcessiveNameLength_ShouldRejectCommand()
    {
        var command = new UpdateCategoryCommand(
            1, new string('A', Category.MaxNameLength + 1));
        await AssertInvalidCommandAsync(command, "Name");
    }

    [Fact]
    public async Task HandleAsync_WithExcessiveDescriptionLength_ShouldRejectCommand()
    {
        var command = new UpdateCategoryCommand(
            1,
            "Technology",
            new string('B', Category.MaxDescriptionLength + 1));
        await AssertInvalidCommandAsync(command, "Description");
    }

    [Fact]
    public async Task HandleAsync_WithMultipleInvalidValues_ShouldReturnAllErrors()
    {
        var command = new UpdateCategoryCommand(
            0,
            "",
            new string('B', Category.MaxDescriptionLength + 1));
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Equal(3, exception.Errors.Count());
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "Id");
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "Name");
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "Description");
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(PersistenceRuleCode.CategoryNotFound)]
    [InlineData(PersistenceRuleCode.DuplicateCategoryName)]
    public async Task HandleAsync_WithPersistenceConflict_ShouldPropagateException(
        PersistenceRuleCode code)
    {
        var command = new UpdateCategoryCommand(1, "Technology");
        var expected = new PersistenceRuleException(
            code, "Persistence rule violation.");
        _repository.Setup(repository => repository.UpdateAsync(
                command.Id,
                command.Name,
                command.Description,
                CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        Assert.Equal(code, exception.Code);
        _repository.Verify(repository => repository.UpdateAsync(
            command.Id,
            command.Name,
            command.Description,
            CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_ShouldNotCallRepository()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var command = new UpdateCategoryCommand(1, "Technology");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(command, cancellation.Token));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_ShouldPropagateException()
    {
        var command = new UpdateCategoryCommand(1, "Technology");
        var expected = new InvalidOperationException(
            "Unexpected persistence failure.");
        _repository.Setup(repository => repository.UpdateAsync(
                command.Id,
                command.Name,
                command.Description,
                CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        _repository.Verify(repository => repository.UpdateAsync(
            command.Id,
            command.Name,
            command.Description,
            CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    private async Task AssertSuccessfulUpdateAsync(
        UpdateCategoryCommand command)
    {
        using var cancellation = new CancellationTokenSource();
        _repository.Setup(repository => repository.UpdateAsync(
                command.Id,
                command.Name,
                command.Description,
                cancellation.Token))
            .Returns(Task.CompletedTask);
        await _handler.HandleAsync(command, cancellation.Token);
        _repository.Verify(repository => repository.UpdateAsync(
            command.Id,
            command.Name,
            command.Description,
            cancellation.Token), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    private async Task AssertInvalidCommandAsync(
        UpdateCategoryCommand command,
        string expectedPropertyName)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        var failure = Assert.Single(exception.Errors);
        Assert.Equal(expectedPropertyName, failure.PropertyName);
        _repository.VerifyNoOtherCalls();
    }
}
