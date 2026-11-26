namespace InventoryManagement.Application.Models;

public sealed record ProductReadModel(
    int Id,
    string Sku,
    string Name,
    string? Description,
    int CategoryId,
    int Stock,
    bool IsActive);
