using FluentValidation;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Application.Products.Commands.UpdateProduct;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Parameters;
using Moq;

namespace InventoryManagement.Application.Tests.Products.Commands.UpdateProduct;

public class UpdateProductCommandHandlerTests
{
    private readonly Mock<IProductWriteRepository> _repository =
        new(MockBehavior.Strict);
    private readonly UpdateProductCommandHandler _handler;

    public UpdateProductCommandHandlerTests()
    {
        _handler = new UpdateProductCommandHandler(
            _repository.Object,
            new UpdateProductCommandValidator());
    }

    [Theory]
    [InlineData("  SKU-002  ", "  Monitor  ", 2, "  External display  ")]
    [InlineData("SKU-002", "Monitor", 1, null)]
    [InlineData("SKU-002", "Monitor", 2, "   ")]
    public async Task HandleAsync_WithValidData_ShouldUpdateProduct(
        string sku,
        string name,
        int categoryId,
        string? description)
    {
        var command = new UpdateProductCommand(
            1, sku, name, categoryId, description);
        await AssertSuccessfulUpdateAsync(command);
    }

    [Fact]
    public async Task HandleAsync_WithMaximumLengths_ShouldUpdateProduct()
    {
        var sku = new string('S', Product.MaxSkuLength);
        var name = new string('N', Product.MaxNameLength);
        var description = new string(
            'D', Product.MaxDescriptionLength);
        var command = new UpdateProductCommand(
            int.MaxValue,
            $"  {sku}  ",
            $"  {name}  ",
            int.MaxValue,
            $"  {description}  ");
        await AssertSuccessfulUpdateAsync(command);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_WithInvalidId_ShouldRejectCommand(int id)
    {
        var command = CreateValidCommand() with { Id = id };
        await AssertInvalidCommandAsync(command, "Id");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_WithInvalidCategoryId_ShouldRejectCommand(
        int categoryId)
    {
        var command = CreateValidCommand() with
        {
            CategoryId = categoryId
        };
        await AssertInvalidCommandAsync(command, "CategoryId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WithInvalidSku_ShouldRejectCommand(
        string? sku)
    {
        var command = CreateValidCommand() with { Sku = sku! };
        await AssertInvalidCommandAsync(command, "Sku");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WithInvalidName_ShouldRejectCommand(
        string? name)
    {
        var command = CreateValidCommand() with { Name = name! };
        await AssertInvalidCommandAsync(command, "Name");
    }

    [Fact]
    public async Task HandleAsync_WithExcessiveSkuLength_ShouldRejectCommand()
    {
        var command = CreateValidCommand() with
        {
            Sku = new string('S', Product.MaxSkuLength + 1)
        };
        await AssertInvalidCommandAsync(command, "Sku");
    }

    [Fact]
    public async Task HandleAsync_WithExcessiveNameLength_ShouldRejectCommand()
    {
        var command = CreateValidCommand() with
        {
            Name = new string('N', Product.MaxNameLength + 1)
        };
        await AssertInvalidCommandAsync(command, "Name");
    }

    [Fact]
    public async Task HandleAsync_WithExcessiveDescriptionLength_ShouldRejectCommand()
    {
        var command = CreateValidCommand() with
        {
            Description = new string(
                'D', Product.MaxDescriptionLength + 1)
        };
        await AssertInvalidCommandAsync(command, "Description");
    }

    [Fact]
    public async Task HandleAsync_WithMultipleInvalidValues_ShouldReturnAllErrors()
    {
        var command = new UpdateProductCommand(
            0, "", "", 0,
            new string('D', Product.MaxDescriptionLength + 1));
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Equal(5, exception.Errors.Count());
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "Id");
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "Sku");
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "Name");
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "CategoryId");
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "Description");
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(PersistenceRuleCode.ProductNotFound)]
    [InlineData(PersistenceRuleCode.CategoryNotFound)]
    [InlineData(PersistenceRuleCode.DuplicateSku)]
    [InlineData(PersistenceRuleCode.ConcurrentProductChange)]
    public async Task HandleAsync_WithPersistenceRuleViolation_ShouldPropagateException(
        PersistenceRuleCode code)
    {
        var command = CreateValidCommand();
        var details = CreateExpectedDetails(command);
        var expected = new PersistenceRuleException(
            code, "Persistence rule violation.");
        _repository.Setup(repository => repository.UpdateAsync(
                command.Id, details, CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        Assert.Equal(code, exception.Code);
        _repository.Verify(repository => repository.UpdateAsync(
            command.Id, details, CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_ShouldNotCallRepository()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var command = CreateValidCommand();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(command, cancellation.Token));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_ShouldPropagateException()
    {
        var command = CreateValidCommand();
        var details = CreateExpectedDetails(command);
        var expected = new InvalidOperationException(
            "Unexpected persistence failure.");
        _repository.Setup(repository => repository.UpdateAsync(
                command.Id, details, CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        _repository.Verify(repository => repository.UpdateAsync(
            command.Id, details, CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    private async Task AssertSuccessfulUpdateAsync(
        UpdateProductCommand command)
    {
        using var cancellation = new CancellationTokenSource();
        var details = CreateExpectedDetails(command);
        _repository.Setup(repository => repository.UpdateAsync(
                command.Id, details, cancellation.Token))
            .Returns(Task.CompletedTask);
        await _handler.HandleAsync(command, cancellation.Token);
        _repository.Verify(repository => repository.UpdateAsync(
            command.Id, details, cancellation.Token), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    private async Task AssertInvalidCommandAsync(
        UpdateProductCommand command,
        string expectedPropertyName)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        var failure = Assert.Single(exception.Errors);
        Assert.Equal(expectedPropertyName, failure.PropertyName);
        _repository.VerifyNoOtherCalls();
    }

    private static UpdateProductCommand CreateValidCommand()
    {
        return new UpdateProductCommand(
            1, "SKU-002", "Monitor", 2, "External display");
    }

    private static ProductDetails CreateExpectedDetails(
        UpdateProductCommand command)
    {
        return new ProductDetails(
            command.Sku,
            command.Name,
            command.CategoryId,
            command.Description);
    }
}
