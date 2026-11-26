using FluentValidation;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Application.InventoryMovements.Commands.RegisterInventoryMovement;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;
using InventoryManagement.Domain.Exceptions;
using Moq;

namespace InventoryManagement.Application.Tests.InventoryMovements.Commands.RegisterInventoryMovement;

public class RegisterInventoryMovementCommandHandlerTests
{
    private static readonly DateTimeOffset MovementDate =
        new DateTimeOffset(
            2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
        .AddTicks(1234567);
    private readonly Mock<IInventoryMovementWriteRepository> _repository =
        new(MockBehavior.Strict);
    private readonly Mock<TimeProvider> _timeProvider =
        new(MockBehavior.Strict);
    private readonly RegisterInventoryMovementCommandHandler _handler;

    public RegisterInventoryMovementCommandHandlerTests()
    {
        _handler = new RegisterInventoryMovementCommandHandler(
            _repository.Object,
            new RegisterInventoryMovementCommandValidator(),
            _timeProvider.Object);
    }

    [Theory]
    [InlineData(MovementType.Entry, 1)]
    [InlineData(MovementType.Exit, 10)]
    [InlineData(MovementType.Entry, int.MaxValue)]
    [InlineData(MovementType.Exit, int.MaxValue)]
    public async Task HandleAsync_WithValidData_ShouldRegisterMovement(
        MovementType type,
        int quantity)
    {
        using var cancellation = new CancellationTokenSource();
        var command = new RegisterInventoryMovementCommand(
            1, type, quantity);
        SetupClock();
        SetupSuccessfulRegistration(command, cancellation.Token);
        var result = await _handler.HandleAsync(
            command, cancellation.Token);
        Assert.Equal(25, result);
        VerifyRegistration(cancellation.Token);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_WithInvalidProductId_ShouldRejectCommand(
        int productId)
    {
        var command = CreateValidCommand() with
        {
            ProductId = productId
        };
        await AssertInvalidCommandAsync(command, "ProductId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleAsync_WithInvalidQuantity_ShouldRejectCommand(
        int quantity)
    {
        var command = CreateValidCommand() with
        {
            Quantity = quantity
        };
        await AssertInvalidCommandAsync(command, "Quantity");
    }

    [Theory]
    [InlineData((MovementType)0)]
    [InlineData((MovementType)3)]
    [InlineData((MovementType)255)]
    public async Task HandleAsync_WithInvalidType_ShouldRejectCommand(
        MovementType type)
    {
        var command = CreateValidCommand() with { Type = type };
        await AssertInvalidCommandAsync(command, "Type");
    }

    [Fact]
    public async Task HandleAsync_WithMultipleInvalidValues_ShouldReturnAllErrors()
    {
        var command = new RegisterInventoryMovementCommand(
            0, (MovementType)0, 0);
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Equal(3, exception.Errors.Count());
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "ProductId");
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "Type");
        Assert.Contains(exception.Errors,
            failure => failure.PropertyName == "Quantity");
        VerifyNoDependencyCalls();
    }

    [Fact]
    public async Task HandleAsync_WithUnavailableProduct_ShouldPropagateNotFound()
    {
        var command = CreateValidCommand();
        var expected = new PersistenceRuleException(
            PersistenceRuleCode.ProductNotFound,
            "The active product was not found.");
        SetupRegistrationFailure(expected);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        Assert.Equal(PersistenceRuleCode.ProductNotFound, exception.Code);
        VerifyRegistration(CancellationToken.None);
    }

    [Theory]
    [InlineData(MovementType.Exit, DomainRuleCode.InsufficientStock)]
    [InlineData(MovementType.Entry, DomainRuleCode.StockLimitExceeded)]
    public async Task HandleAsync_WithDomainRuleViolation_ShouldPropagateConflict(
        MovementType type,
        DomainRuleCode code)
    {
        var command = CreateValidCommand() with { Type = type };
        var expected = new DomainRuleException(
            code, "Inventory rule violation.");
        SetupRegistrationFailure(expected);
        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        Assert.Equal(code, exception.Code);
        VerifyRegistration(CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WithCancellation_ShouldNotCallDependencies()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var command = CreateValidCommand();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _handler.HandleAsync(command, cancellation.Token));
        VerifyNoDependencyCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_ShouldPropagateException()
    {
        var command = CreateValidCommand();
        var expected = new InvalidOperationException(
            "Unexpected persistence failure.");
        SetupRegistrationFailure(expected);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        Assert.Same(expected, exception);
        VerifyRegistration(CancellationToken.None);
    }

    private void SetupClock()
    {
        _timeProvider.Setup(provider => provider.GetUtcNow())
            .Returns(MovementDate);
    }

    private void SetupSuccessfulRegistration(
        RegisterInventoryMovementCommand command,
        CancellationToken cancellationToken)
    {
        _repository.Setup(repository => repository.RegisterAsync(
                It.Is<InventoryMovement>(movement =>
                    movement.Id == 0 &&
                    movement.ProductId == command.ProductId &&
                    movement.Type == command.Type &&
                    movement.Quantity == command.Quantity &&
                    movement.CreatedAt.Ticks ==
                        MovementDate.UtcDateTime.Ticks &&
                    movement.CreatedAt.Kind == DateTimeKind.Utc),
                cancellationToken))
            .ReturnsAsync(25);
    }

    private void SetupRegistrationFailure(Exception exception)
    {
        SetupClock();
        _repository.Setup(repository => repository.RegisterAsync(
                It.IsAny<InventoryMovement>(),
                CancellationToken.None))
            .ThrowsAsync(exception);
    }

    private void VerifyRegistration(
        CancellationToken cancellationToken)
    {
        _repository.Verify(repository => repository.RegisterAsync(
            It.IsAny<InventoryMovement>(),
            cancellationToken), Times.Once());
        _timeProvider.Verify(
            provider => provider.GetUtcNow(), Times.Once());
        VerifyNoDependencyCalls();
    }

    private void VerifyNoDependencyCalls()
    {
        _repository.VerifyNoOtherCalls();
        _timeProvider.VerifyNoOtherCalls();
    }

    private async Task AssertInvalidCommandAsync(
        RegisterInventoryMovementCommand command,
        string expectedPropertyName)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
        var failure = Assert.Single(exception.Errors);
        Assert.Equal(expectedPropertyName, failure.PropertyName);
        VerifyNoDependencyCalls();
    }

    private static RegisterInventoryMovementCommand CreateValidCommand()
    {
        return new RegisterInventoryMovementCommand(
            1, MovementType.Entry, 10);
    }
}
