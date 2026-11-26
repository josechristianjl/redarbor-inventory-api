namespace InventoryManagement.Application.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    int Id,
    string Sku,
    string Name,
    int CategoryId,
    string? Description = null);
