using FluentValidation;
using InventoryManagement.Application.Products.Validation;

namespace InventoryManagement.Application.Products.Queries.GetProductById;

public sealed class GetProductByIdQueryValidator
    : AbstractValidator<GetProductByIdQuery>
{
    public GetProductByIdQueryValidator()
    {
        RuleFor(query => query.Id)
            .ValidProductId();
    }
}
