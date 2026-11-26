namespace InventoryManagement.Application.Models;

public sealed record CategoryReadModel(
    int Id,
    string Name,
    string? Description,
    bool IsActive);
