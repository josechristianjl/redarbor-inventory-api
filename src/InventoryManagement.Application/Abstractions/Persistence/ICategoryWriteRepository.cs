using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Parameters;

namespace InventoryManagement.Application.Abstractions.Persistence;

public interface ICategoryWriteRepository
{
    Task<int> CreateAsync(
        Category category,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        int id,
        CategoryDetails details,
        CancellationToken cancellationToken);

    Task DeactivateAsync(
        int id,
        CancellationToken cancellationToken);
}
