namespace InventoryManagement.Application.Models;

public sealed record PageRequest(
    int Page = 1,
    int PageSize = 20)
{
    public const int MaxPageSize = 100;
}
