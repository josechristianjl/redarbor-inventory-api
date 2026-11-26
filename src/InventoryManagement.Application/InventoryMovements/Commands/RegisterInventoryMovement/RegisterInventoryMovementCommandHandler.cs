using FluentValidation;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.InventoryMovements.Commands.RegisterInventoryMovement;

public sealed class RegisterInventoryMovementCommandHandler
    : ICommandHandler<RegisterInventoryMovementCommand, int>
{
    private readonly IInventoryMovementWriteRepository _repository;
    private readonly IValidator<RegisterInventoryMovementCommand> _validator;
    private readonly TimeProvider _timeProvider;

    public RegisterInventoryMovementCommandHandler(
        IInventoryMovementWriteRepository repository,
        IValidator<RegisterInventoryMovementCommand> validator,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _validator = validator;
        _timeProvider = timeProvider;
    }
    public async Task<int> HandleAsync(
        RegisterInventoryMovementCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        await _validator.ValidateAndThrowAsync(
            command, cancellationToken);
        var movement = new InventoryMovement(
            command.ProductId,
            command.Type,
            command.Quantity,
            _timeProvider.GetUtcNow().UtcDateTime);

        return await _repository.RegisterAsync(
            movement, cancellationToken);
    }
}
