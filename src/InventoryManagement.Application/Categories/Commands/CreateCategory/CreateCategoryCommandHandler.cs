using FluentValidation;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Categories.Commands.CreateCategory;

public sealed class CreateCategoryCommandHandler
    : ICommandHandler<CreateCategoryCommand, int>
{
    private readonly ICategoryWriteRepository _repository;
    private readonly IValidator<CreateCategoryCommand> _validator;

    public CreateCategoryCommandHandler(
        ICategoryWriteRepository repository,
        IValidator<CreateCategoryCommand> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<int> HandleAsync(
        CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        await _validator.ValidateAndThrowAsync(
            command, cancellationToken);
        var category = new Category(
            command.Name, command.Description);

        return await _repository.CreateAsync(
            category, cancellationToken);
    }
}
