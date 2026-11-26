using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Application.Models;
using InventoryManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Infrastructure.Persistence.Pagination;

namespace InventoryManagement.Infrastructure.Persistence.Repositories.Read;

public sealed class CategoryReadRepository : ICategoryReadRepository
{
    private readonly InventoryDbContext _context;
    public CategoryReadRepository(InventoryDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<CategoryReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await Project(GetActiveCategories().Where(category => category.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<PagedResult<CategoryReadModel>> GetPagedAsync(
    PageRequest request,
    CancellationToken cancellationToken)
    {
        var query = Project(GetActiveCategories()
            .OrderBy(category => category.Id));

        return PaginationHelper.CreateAsync(
            query, request, cancellationToken);
    }

    private IQueryable<Category> GetActiveCategories()
    {
        return _context.Categories
            .AsNoTracking()
            .Where(category => category.IsActive);
    }

    private static IQueryable<CategoryReadModel> Project(
        IQueryable<Category> query)
    {
        return query.Select(category => new CategoryReadModel(
            category.Id,
            category.Name,
            category.Description,
            category.IsActive));
    }
}
