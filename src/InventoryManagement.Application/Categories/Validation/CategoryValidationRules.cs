using FluentValidation;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Categories.Validation;

internal static class CategoryValidationRules
{
    internal static IRuleBuilderOptions<T, string> ValidCategoryName<T>(
        this IRuleBuilderInitial<T, string> rule)
    {
        return rule
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Category name is required.")
            .Must(name => name is not null &&
                name.Trim().Length <= Category.MaxNameLength)
            .WithMessage(
                $"Category name must not exceed {Category.MaxNameLength} characters.");
    }

    internal static IRuleBuilderOptions<T, string?> ValidCategoryDescription<T>(
        this IRuleBuilderInitial<T, string?> rule)
    {
        return rule
            .Must(description => description is null ||
                description.Trim().Length <= Category.MaxDescriptionLength)
            .WithMessage(
                $"Category description must not exceed {Category.MaxDescriptionLength} characters.");
    }

    internal static IRuleBuilderOptions<T, int> ValidCategoryId<T>(
        this IRuleBuilderInitial<T, int> rule)
    {
        return rule
            .GreaterThan(0)
            .WithMessage("Category identifier must be greater than zero.");
    }
}
