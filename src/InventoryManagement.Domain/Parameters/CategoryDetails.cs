namespace InventoryManagement.Domain.Parameters;

public sealed record CategoryDetails(
    string Name,
    string? Description = null);
