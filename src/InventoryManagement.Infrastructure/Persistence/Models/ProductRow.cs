namespace InventoryManagement.Infrastructure.Persistence.Models;

internal sealed record ProductRow(
    int Id,
    int CategoryId,
    string SKU,
    string Name,
    string? Description,
    int Stock,
    bool IsActive);
