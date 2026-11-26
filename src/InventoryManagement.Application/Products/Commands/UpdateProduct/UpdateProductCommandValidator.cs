using FluentValidation;
using InventoryManagement.Application.Products.Validation;

namespace InventoryManagement.Application.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommandValidator
    : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(command => command.Id)
            .ValidProductId();
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
