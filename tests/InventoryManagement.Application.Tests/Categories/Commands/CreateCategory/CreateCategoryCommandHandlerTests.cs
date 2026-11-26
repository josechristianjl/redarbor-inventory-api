using FluentValidation;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Categories.Commands.CreateCategory;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Domain.Entities;
using Moq;

namespace InventoryManagement.Application.Tests.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandlerTests
{
    private readonly Mock<ICategoryWriteRepository> _repository =
        new(MockBehavior.Strict);
    private readonly CreateCategoryCommandHandler _handler;

    public CreateCategoryCommandHandlerTests()
    {
        _handler = new CreateCategoryCommandHandler(
            _repository.Object,
            new CreateCategoryCommandValidator());
    }

    [Theory]
    [InlineData("  Electronics  ", "  Electronic devices  ", "Electronic devices")]
    [InlineData("Electronics", null, null)]
    [InlineData("Electronics", "   ", null)]
    public async Task HandleAsync_WithValidData_ShouldCreateNormalizedCategory(
        string name,
        string? description,
        string? expectedDescription)
    {
        var command = new CreateCategoryCommand(name, description);
        await AssertSuccessfulCreationAsync(
            command, "Electronics", expectedDescription);
    }

    [Fact]
    public async Task HandleAsync_WithMaximumLengths_ShouldCreateCategory()
    {
        var name = new string('A', Category.MaxNameLength);
        var description = new string('B', Category.MaxDescriptionLength);
        var command = new CreateCategoryCommand(
            $"  {name}  ", $"  {description}  ");
        await AssertSuccessfulCreationAsync(command, name, description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WithInvalidName_ShouldRejectCommand(
        string? name)
    {
        var command = new CreateCategoryCommand(name!);
        await AssertInvalidCommandAsync(command, "Name");
    }

    [Fact]
    public async Task HandleAsync_WithExcessiveNameLength_ShouldRejectCommand()
    {
        var command = new CreateCategoryCommand(
            new string('A', Category.MaxNameLength + 1));
        await AssertInvalidCommandAsync(command, "Name");
    }

    [Fact]
    public async Task HandleAsync_WithExcessiveDescriptionLength_ShouldRejectCommand()
    {
        var command = new CreateCategoryCommand(
            "Electronics",
            new string('B', Category.MaxDescriptionLength + 1));
        await AssertInvalidCommandAsync(command, "Description");
    }

    [Fact]
    public async Task HandleAsync_WithMultipleInvalidValues_ShouldReturnAllErrors()
    {
        var command = new CreateCategoryCommand(
            "",
            new string('B', Category.MaxDescriptionLength + 1));
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Equal(2, exception.Errors.Count());
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "Name");
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "Description");
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateName_ShouldPropagateConflict()
    {
        var command = new CreateCategoryCommand("Electronics");
        var expected = new PersistenceRuleException(
            PersistenceRuleCode.DuplicateCategoryName,
            "A category with the same name already exists.");
        _repository.Setup(repository => repository.CreateAsync(
                It.IsAny<Category>(), CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        Assert.Equal(PersistenceRuleCode.DuplicateCategoryName, exception.Code);
        _repository.Verify(repository => repository.CreateAsync(
            It.IsAny<Category>(), CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_ShouldNotCallRepository()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var command = new CreateCategoryCommand("Electronics");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(command, cancellation.Token));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_ShouldPropagateException()
    {
        var command = new CreateCategoryCommand("Electronics");
        var expected = new InvalidOperationException(
            "Unexpected persistence failure.");
        _repository.Setup(repository => repository.CreateAsync(
                It.IsAny<Category>(), CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        _repository.Verify(repository => repository.CreateAsync(
            It.IsAny<Category>(), CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    private async Task AssertSuccessfulCreationAsync(
        CreateCategoryCommand command,
        string expectedName,
        string? expectedDescription)
    {
        using var cancellation = new CancellationTokenSource();
        _repository.Setup(repository => repository.CreateAsync(
                It.Is<Category>(category =>
                    category.Id == 0 &&
                    category.Name == expectedName &&
                    category.Description == expectedDescription &&
                    category.IsActive),
                cancellation.Token))
            .ReturnsAsync(25);
        var result = await _handler.HandleAsync(
            command, cancellation.Token);
        Assert.Equal(25, result);
        _repository.Verify(repository => repository.CreateAsync(
            It.Is<Category>(category =>
                category.Id == 0 &&
                category.Name == expectedName &&
                category.Description == expectedDescription &&
                category.IsActive),
            cancellation.Token), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    private async Task AssertInvalidCommandAsync(
        CreateCategoryCommand command,
        string expectedPropertyName)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        var failure = Assert.Single(exception.Errors);
        Assert.Equal(expectedPropertyName, failure.PropertyName);
        _repository.VerifyNoOtherCalls();
    }
}
