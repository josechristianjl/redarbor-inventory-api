namespace InventoryManagement.Domain.Parameters;

public sealed record ProductDetails(
    string Sku,
    string Name,
    int CategoryId,
    string? Description = null);
