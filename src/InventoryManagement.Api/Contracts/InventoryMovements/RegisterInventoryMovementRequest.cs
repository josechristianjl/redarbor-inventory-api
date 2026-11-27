using System.Text.Json.Serialization;

namespace InventoryManagement.Api.Contracts.InventoryMovements;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterInventoryMovementRequest
{
    public int ProductId { get; init; }
    public string? Type { get; init; }
    public int Quantity { get; init; }
}
