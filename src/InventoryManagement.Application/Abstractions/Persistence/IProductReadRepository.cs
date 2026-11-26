using InventoryManagement.Application.Models;

namespace InventoryManagement.Application.Abstractions.Persistence;

public interface IProductReadRepository
{
    Task<ProductReadModel?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);

    Task<PagedResult<ProductReadModel>> GetPagedAsync(
        PageRequest request,
        CancellationToken cancellationToken);
}
