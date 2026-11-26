using FluentValidation;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Abstractions.Persistence;

namespace InventoryManagement.Application.Products.Commands.DeleteProduct;

public sealed class DeleteProductCommandHandler
    : ICommandHandler<DeleteProductCommand>
{
    private readonly IProductWriteRepository _repository;
    private readonly IValidator<DeleteProductCommand> _validator;

    public DeleteProductCommandHandler(
        IProductWriteRepository repository,
        IValidator<DeleteProductCommand> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task HandleAsync(
        DeleteProductCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        await _validator.ValidateAndThrowAsync(
            command, cancellationToken);
        await _repository.DeactivateAsync(
            command.Id, cancellationToken);
    }
}
