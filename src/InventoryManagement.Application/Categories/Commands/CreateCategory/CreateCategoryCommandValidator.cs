using FluentValidation;
using InventoryManagement.Application.Categories.Validation;

namespace InventoryManagement.Application.Categories.Commands.CreateCategory;

public sealed class CreateCategoryCommandValidator
    : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(command => command.Name)
            .ValidCategoryName();
        RuleFor(command => command.Description)
            .ValidCategoryDescription();
    }
}
