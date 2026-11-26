using FluentValidation;
using InventoryManagement.Application.Models;

namespace InventoryManagement.Application.Validation;

public sealed class PageRequestValidator
    : AbstractValidator<PageRequest>
{
    public PageRequestValidator()
    {
        RuleFor(request => request.Page)
            .GreaterThan(0)
            .WithMessage("Page must be greater than zero.");
        RuleFor(request => request.PageSize)
            .InclusiveBetween(1, PageRequest.MaxPageSize)
            .WithMessage(
                $"Page size must be between 1 and {PageRequest.MaxPageSize}.");
    }
}
