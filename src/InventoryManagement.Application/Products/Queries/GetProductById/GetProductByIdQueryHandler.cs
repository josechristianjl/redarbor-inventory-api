using FluentValidation;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Application.Models;

namespace InventoryManagement.Application.Products.Queries.GetProductById;

public sealed class GetProductByIdQueryHandler
    : IQueryHandler<GetProductByIdQuery, ProductReadModel>
{
    private readonly IProductReadRepository _repository;
    private readonly IValidator<GetProductByIdQuery> _validator;

    public GetProductByIdQueryHandler(
        IProductReadRepository repository,
        IValidator<GetProductByIdQuery> validator)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(validator);
        _repository = repository;
        _validator = validator;
    }

    public async Task<ProductReadModel> HandleAsync(
        GetProductByIdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        await _validator.ValidateAndThrowAsync(
            query, cancellationToken);
        var product = await _repository.GetByIdAsync(
            query.Id, cancellationToken);

        return product ?? throw new PersistenceRuleException(
            PersistenceRuleCode.ProductNotFound,
            "The active product was not found.");
    }
}
