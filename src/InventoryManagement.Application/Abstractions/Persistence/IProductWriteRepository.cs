using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Parameters;

namespace InventoryManagement.Application.Abstractions.Persistence;

public interface IProductWriteRepository
{
    Task<int> CreateAsync(
        Product product,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        int id,
        ProductDetails details,
        CancellationToken cancellationToken);

    Task DeactivateAsync(
        int id,
        CancellationToken cancellationToken);
}
