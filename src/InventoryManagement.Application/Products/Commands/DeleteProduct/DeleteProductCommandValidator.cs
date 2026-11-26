using FluentValidation;
using InventoryManagement.Application.Products.Validation;

namespace InventoryManagement.Application.Products.Commands.DeleteProduct;

public sealed class DeleteProductCommandValidator
    : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductCommandValidator()
    {
        RuleFor(command => command.Id)
            .ValidProductId();
    }
}
