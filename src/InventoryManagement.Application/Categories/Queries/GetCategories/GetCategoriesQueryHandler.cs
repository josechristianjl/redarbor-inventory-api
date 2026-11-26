using FluentValidation;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Models;

namespace InventoryManagement.Application.Categories.Queries.GetCategories;

public sealed class GetCategoriesQueryHandler
    : IQueryHandler<GetCategoriesQuery, PagedResult<CategoryReadModel>>
{
    private readonly ICategoryReadRepository _repository;

    private readonly IValidator<PageRequest> _validator;

    public GetCategoriesQueryHandler(
        ICategoryReadRepository repository,
        IValidator<PageRequest> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<PagedResult<CategoryReadModel>> HandleAsync(
        GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Pagination);
        cancellationToken.ThrowIfCancellationRequested();
        await _validator.ValidateAndThrowAsync(
            query.Pagination, cancellationToken);

        return await _repository.GetPagedAsync(
            query.Pagination, cancellationToken);
    }
}
