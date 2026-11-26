using FluentValidation;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Abstractions.Persistence;

namespace InventoryManagement.Application.Categories.Commands.UpdateCategory;

public sealed class UpdateCategoryCommandHandler
    : ICommandHandler<UpdateCategoryCommand>
{
    private readonly ICategoryWriteRepository _repository;
    private readonly IValidator<UpdateCategoryCommand> _validator;

    public UpdateCategoryCommandHandler(
        ICategoryWriteRepository repository,
        IValidator<UpdateCategoryCommand> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task HandleAsync(
        UpdateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        await _validator.ValidateAndThrowAsync(
            command, cancellationToken);
        await _repository.UpdateAsync(
            command.Id,
            command.Name,
            command.Description,
            cancellationToken);
    }
}
