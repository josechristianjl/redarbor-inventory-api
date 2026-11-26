using FluentValidation;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Abstractions.Persistence;

namespace InventoryManagement.Application.Categories.Commands.DeleteCategory;

public sealed class DeleteCategoryCommandHandler
    : ICommandHandler<DeleteCategoryCommand>
{
    private readonly ICategoryWriteRepository _repository;
    private readonly IValidator<DeleteCategoryCommand> _validator;

    public DeleteCategoryCommandHandler(
        ICategoryWriteRepository repository,
        IValidator<DeleteCategoryCommand> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task HandleAsync(
        DeleteCategoryCommand command,
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
