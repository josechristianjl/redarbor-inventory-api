using FluentValidation;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Domain.Parameters;

namespace InventoryManagement.Application.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommandHandler
    : ICommandHandler<UpdateProductCommand>
{
    private readonly IProductWriteRepository _repository;
    private readonly IValidator<UpdateProductCommand> _validator;

    public UpdateProductCommandHandler(
        IProductWriteRepository repository,
        IValidator<UpdateProductCommand> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task HandleAsync(
        UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        await _validator.ValidateAndThrowAsync(
            command, cancellationToken);
        var details = new ProductDetails(
            command.Sku,
            command.Name,
            command.CategoryId,
            command.Description);
        await _repository.UpdateAsync(
            command.Id, details, cancellationToken);
    }
}
