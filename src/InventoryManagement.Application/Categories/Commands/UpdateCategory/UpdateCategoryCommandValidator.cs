using FluentValidation;
using InventoryManagement.Application.Categories.Validation;

namespace InventoryManagement.Application.Categories.Commands.UpdateCategory;

public sealed class UpdateCategoryCommandValidator
    : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(command => command.Id)
            .ValidCategoryId();
        RuleFor(command => command.Name)
            .ValidCategoryName();
        RuleFor(command => command.Description)
            .ValidCategoryDescription();
    }
}
