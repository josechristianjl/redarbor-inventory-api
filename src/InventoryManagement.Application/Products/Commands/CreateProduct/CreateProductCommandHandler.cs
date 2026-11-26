using FluentValidation;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Parameters;

namespace InventoryManagement.Application.Products.Commands.CreateProduct;

public sealed class CreateProductCommandHandler
    : ICommandHandler<CreateProductCommand, int>
{
    private readonly IProductWriteRepository _repository;
    private readonly IValidator<CreateProductCommand> _validator;

    public CreateProductCommandHandler(
        IProductWriteRepository repository,
        IValidator<CreateProductCommand> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<int> HandleAsync(
        CreateProductCommand command,
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
        var product = new Product(details);

        return await _repository.CreateAsync(
            product, cancellationToken);
    }
}
