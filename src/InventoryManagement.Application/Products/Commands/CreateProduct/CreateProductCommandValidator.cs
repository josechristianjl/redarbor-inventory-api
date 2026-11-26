using FluentValidation;
using InventoryManagement.Application.Products.Validation;

namespace InventoryManagement.Application.Products.Commands.CreateProduct;

public sealed class CreateProductCommandValidator
    : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.Sku)
            .ValidProductSku();
        RuleFor(command => command.Name)
            .ValidProductName();
        RuleFor(command => command.CategoryId)
            .ValidProductCategoryId();
        RuleFor(command => command.Description)
            .ValidProductDescription();
    }
}
