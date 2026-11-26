namespace InventoryManagement.Application.Categories.Commands.UpdateCategory;

public sealed record UpdateCategoryCommand(
    int Id,
    string Name,
    string? Description = null);
