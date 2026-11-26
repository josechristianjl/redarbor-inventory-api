using FluentValidation;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Models;

namespace InventoryManagement.Application.Products.Queries.GetProducts;

public sealed class GetProductsQueryHandler
    : IQueryHandler<GetProductsQuery, PagedResult<ProductReadModel>>
{
    private readonly IProductReadRepository _repository;
    private readonly IValidator<PageRequest> _validator;

    public GetProductsQueryHandler(
        IProductReadRepository repository,
        IValidator<PageRequest> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<PagedResult<ProductReadModel>> HandleAsync(
        GetProductsQuery query,
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
