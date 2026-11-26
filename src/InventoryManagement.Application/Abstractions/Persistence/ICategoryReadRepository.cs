using InventoryManagement.Application.Models;

namespace InventoryManagement.Application.Abstractions.Persistence;

public interface ICategoryReadRepository
{
    Task<CategoryReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<PagedResult<CategoryReadModel>> GetPagedAsync(
        PageRequest request,
        CancellationToken cancellationToken);
}
