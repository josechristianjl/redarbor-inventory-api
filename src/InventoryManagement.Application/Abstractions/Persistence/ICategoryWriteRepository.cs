using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Abstractions.Persistence;

public interface ICategoryWriteRepository
{
    Task<int> CreateAsync(
        Category category,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        int id,
        string name,
        string? description,
        CancellationToken cancellationToken);

    Task DeactivateAsync(
        int id,
        CancellationToken cancellationToken);
}
