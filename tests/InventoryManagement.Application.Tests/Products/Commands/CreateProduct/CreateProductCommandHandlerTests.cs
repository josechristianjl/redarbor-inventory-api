using FluentValidation;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Application.Products.Commands.CreateProduct;
using InventoryManagement.Domain.Entities;
using Moq;

namespace InventoryManagement.Application.Tests.Products.Commands.CreateProduct;

public class CreateProductCommandHandlerTests
{
    private readonly Mock<IProductWriteRepository> _repository =
        new(MockBehavior.Strict);
    private readonly CreateProductCommandHandler _handler;

    public CreateProductCommandHandlerTests()
    {
        _handler = new CreateProductCommandHandler(
            _repository.Object,
            new CreateProductCommandValidator());
    }

    [Theory]
    [InlineData("  SKU-001  ", "  Laptop  ", "  Portable computer  ", "Portable computer")]
    [InlineData("SKU-001", "Laptop", null, null)]
    [InlineData("SKU-001", "Laptop", "   ", null)]
    public async Task HandleAsync_WithValidData_ShouldCreateNormalizedProduct(
        string sku,
        string name,
        string? description,
        string? expectedDescription)
    {
        var command = new CreateProductCommand(
            sku, name, 1, description);
        await AssertSuccessfulCreationAsync(
            command, "SKU-001", "Laptop", expectedDescription);
    }

    [Fact]
    public async Task HandleAsync_WithMaximumLengths_ShouldCreateProduct()
    {
        var sku = new string('S', Product.MaxSkuLength);
        var name = new string('N', Product.MaxNameLength);
        var description = new string(
            'D', Product.MaxDescriptionLength);
        var command = new CreateProductCommand(
            $"  {sku}  ",
            $"  {name}  ",
            int.MaxValue,
            $"  {description}  ");
        await AssertSuccessfulCreationAsync(
            command, sku, name, description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WithInvalidSku_ShouldRejectCommand(
        string? sku)
    {
        var command = new CreateProductCommand(
            sku!, "Laptop", 1);
        await AssertInvalidCommandAsync(command, "Sku");
    }

    [Fact]
    public async Task HandleAsync_WithExcessiveSkuLength_ShouldRejectCommand()
    {
        var command = new CreateProductCommand(
            new string('S', Product.MaxSkuLength + 1),
            "Laptop", 1);
        await AssertInvalidCommandAsync(command, "Sku");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WithInvalidName_ShouldRejectCommand(
        string? name)
    {
        var command = new CreateProductCommand(
            "SKU-001", name!, 1);
        await AssertInvalidCommandAsync(command, "Name");
    }

    [Fact]
    public async Task HandleAsync_WithExcessiveNameLength_ShouldRejectCommand()
    {
        var command = new CreateProductCommand(
            "SKU-001",
            new string('N', Product.MaxNameLength + 1),
            1);
        await AssertInvalidCommandAsync(command, "Name");
    }

    [Fact]
    public async Task HandleAsync_WithExcessiveDescriptionLength_ShouldRejectCommand()
    {
        var command = new CreateProductCommand(
            "SKU-001",
            "Laptop",
            1,
            new string('D', Product.MaxDescriptionLength + 1));
        await AssertInvalidCommandAsync(command, "Description");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_WithInvalidCategoryId_ShouldRejectCommand(
        int categoryId)
    {
        var command = new CreateProductCommand(
            "SKU-001", "Laptop", categoryId);
        await AssertInvalidCommandAsync(command, "CategoryId");
    }

    [Fact]
    public async Task HandleAsync_WithMultipleInvalidValues_ShouldReturnAllErrors()
    {
        var command = new CreateProductCommand(
            "",
            "",
            0,
            new string('D', Product.MaxDescriptionLength + 1));
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Equal(4, exception.Errors.Count());
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
    [InlineData(PersistenceRuleCode.DuplicateSku)]
    [InlineData(PersistenceRuleCode.CategoryNotFound)]
    public async Task HandleAsync_WithPersistenceRuleViolation_ShouldPropagateException(
        PersistenceRuleCode code)
    {
        var command = new CreateProductCommand(
            "SKU-001", "Laptop", 1);
        var expected = new PersistenceRuleException(
            code, "Persistence rule violation.");
        _repository.Setup(repository => repository.CreateAsync(
                It.IsAny<Product>(), CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        Assert.Equal(code, exception.Code);
        _repository.Verify(repository => repository.CreateAsync(
            It.IsAny<Product>(), CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_ShouldNotCallRepository()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var command = new CreateProductCommand(
            "SKU-001", "Laptop", 1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(command, cancellation.Token));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_ShouldPropagateException()
    {
        var command = new CreateProductCommand(
            "SKU-001", "Laptop", 1);
        var expected = new InvalidOperationException(
            "Unexpected persistence failure.");
        _repository.Setup(repository => repository.CreateAsync(
                It.IsAny<Product>(), CancellationToken.None))
            .ThrowsAsync(expected);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        _repository.Verify(repository => repository.CreateAsync(
            It.IsAny<Product>(), CancellationToken.None), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    private async Task AssertSuccessfulCreationAsync(
        CreateProductCommand command,
        string expectedSku,
        string expectedName,
        string? expectedDescription)
    {
        using var cancellation = new CancellationTokenSource();
        _repository.Setup(repository => repository.CreateAsync(
                It.Is<Product>(product =>
                    product.Id == 0 &&
                    product.SKU == expectedSku &&
                    product.Name == expectedName &&
                    product.Description == expectedDescription &&
                    product.CategoryId == command.CategoryId &&
                    product.Stock == 0 &&
                    product.IsActive),
                cancellation.Token))
            .ReturnsAsync(25);
        var result = await _handler.HandleAsync(
            command, cancellation.Token);
        Assert.Equal(25, result);
        _repository.Verify(repository => repository.CreateAsync(
            It.Is<Product>(product =>
                product.Id == 0 &&
                product.SKU == expectedSku &&
                product.Name == expectedName &&
                product.Description == expectedDescription &&
                product.CategoryId == command.CategoryId &&
                product.Stock == 0 &&
                product.IsActive),
            cancellation.Token), Times.Once());
        _repository.VerifyNoOtherCalls();
    }

    private async Task AssertInvalidCommandAsync(
        CreateProductCommand command,
        string expectedPropertyName)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        var failure = Assert.Single(exception.Errors);
        Assert.Equal(expectedPropertyName, failure.PropertyName);
        _repository.VerifyNoOtherCalls();
    }
}
