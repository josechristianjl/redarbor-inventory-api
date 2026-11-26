using InventoryManagement.Domain.Entities;

namespace InventoryManagement.Application.Abstractions.Persistence;

public interface IInventoryMovementWriteRepository
{
    Task<int> RegisterAsync(
        InventoryMovement movement,
        CancellationToken cancellationToken);
}
