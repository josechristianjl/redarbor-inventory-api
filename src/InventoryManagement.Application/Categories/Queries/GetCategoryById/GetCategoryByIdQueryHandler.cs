using FluentValidation;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Application.Models;

namespace InventoryManagement.Application.Categories.Queries.GetCategoryById;

public sealed class GetCategoryByIdQueryHandler
    : IQueryHandler<GetCategoryByIdQuery, CategoryReadModel>
{
    private readonly ICategoryReadRepository _repository;
    private readonly IValidator<GetCategoryByIdQuery> _validator;

    public GetCategoryByIdQueryHandler(
        ICategoryReadRepository repository,
        IValidator<GetCategoryByIdQuery> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<CategoryReadModel> HandleAsync(
        GetCategoryByIdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        await _validator.ValidateAndThrowAsync(
            query, cancellationToken);
        var category = await _repository.GetByIdAsync(
            query.Id, cancellationToken);

        return category ?? throw new PersistenceRuleException(
            PersistenceRuleCode.CategoryNotFound,
            "The active category was not found.");
    }
}
