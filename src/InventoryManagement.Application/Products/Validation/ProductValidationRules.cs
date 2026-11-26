using FluentValidation;
using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Products.Validation;

internal static class ProductValidationRules
{
    internal static IRuleBuilderOptions<T, int> ValidProductId<T>(
        this IRuleBuilderInitial<T, int> rule)
    {
        return rule
            .GreaterThan(0)
            .WithMessage("Product identifier must be greater than zero.");
    }

    internal static IRuleBuilderOptions<T, string> ValidProductSku<T>(
        this IRuleBuilderInitial<T, string> rule)
    {
        return rule
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Product SKU is required.")
            .Must(sku => sku is not null &&
                sku.Trim().Length <= Product.MaxSkuLength)
            .WithMessage(
                $"Product SKU must not exceed {Product.MaxSkuLength} characters.");
    }

    internal static IRuleBuilderOptions<T, string> ValidProductName<T>(
        this IRuleBuilderInitial<T, string> rule)
    {
        return rule
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Product name is required.")
            .Must(name => name is not null &&
                name.Trim().Length <= Product.MaxNameLength)
            .WithMessage(
                $"Product name must not exceed {Product.MaxNameLength} characters.");
    }

    internal static IRuleBuilderOptions<T, int> ValidProductCategoryId<T>(
        this IRuleBuilderInitial<T, int> rule)
    {
        return rule
            .GreaterThan(0)
            .WithMessage("Category identifier must be greater than zero.");
    }

    internal static IRuleBuilderOptions<T, string?> ValidProductDescription<T>(
        this IRuleBuilderInitial<T, string?> rule)
    {
        return rule
            .Must(description => description is null ||
                description.Trim().Length <= Product.MaxDescriptionLength)
            .WithMessage(
                $"Product description must not exceed {Product.MaxDescriptionLength} characters.");
    }
}
