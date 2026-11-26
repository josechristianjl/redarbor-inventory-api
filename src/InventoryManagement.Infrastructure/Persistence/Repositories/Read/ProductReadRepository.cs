using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Models;
using InventoryManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Infrastructure.Persistence.Pagination;

namespace InventoryManagement.Infrastructure.Persistence.Repositories.Read;

public sealed class ProductReadRepository : IProductReadRepository
{
    private readonly InventoryDbContext _context;

    public ProductReadRepository(InventoryDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<ProductReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await Project(GetActiveProducts().Where(product => product.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<PagedResult<ProductReadModel>> GetPagedAsync(
    PageRequest request,
    CancellationToken cancellationToken)
    {
        var query = Project(GetActiveProducts()
            .OrderBy(product => product.Id));

        return PaginationHelper.CreateAsync(
            query, request, cancellationToken);
    }

    private IQueryable<Product> GetActiveProducts()
    {
        return _context.Products
            .AsNoTracking()
            .Where(product => product.IsActive);
    }

    private static IQueryable<ProductReadModel> Project(
        IQueryable<Product> query)
    {
        return query.Select(product => new ProductReadModel(
            product.Id,
            product.SKU,
            product.Name,
            product.Description,
            product.CategoryId,
            product.Stock,
            product.IsActive));
    }
}
