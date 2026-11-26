using FluentValidation;
using InventoryManagement.Application.Categories.Validation;

namespace InventoryManagement.Application.Categories.Queries.GetCategoryById;

public sealed class GetCategoryByIdQueryValidator
    : AbstractValidator<GetCategoryByIdQuery>
{
    public GetCategoryByIdQueryValidator()
    {
        RuleFor(query => query.Id)
            .ValidCategoryId();
    }
}
