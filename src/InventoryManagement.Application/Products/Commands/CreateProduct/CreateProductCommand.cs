namespace InventoryManagement.Application.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Sku,
    string Name,
    int CategoryId,
    string? Description = null);
