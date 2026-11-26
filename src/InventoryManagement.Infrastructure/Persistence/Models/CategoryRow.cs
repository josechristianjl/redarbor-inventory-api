namespace InventoryManagement.Infrastructure.Persistence.Models;

internal sealed record CategoryRow(
    int Id,
    string Name,
    string? Description,
    bool IsActive);
