using FluentValidation;
using InventoryManagement.Application.Categories.Validation;

namespace InventoryManagement.Application.Categories.Commands.DeleteCategory;

public sealed class DeleteCategoryCommandValidator
    : AbstractValidator<DeleteCategoryCommand>
{
    public DeleteCategoryCommandValidator()
    {
        RuleFor(command => command.Id)
            .ValidCategoryId();
    }
}
