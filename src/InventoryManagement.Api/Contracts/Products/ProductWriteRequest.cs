using System.Text.Json.Serialization;

namespace InventoryManagement.Api.Contracts.Products;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProductWriteRequest
{
    public string? Sku { get; init; }
    public string? Name { get; init; }
    public int CategoryId { get; init; }
    public string? Description { get; init; }
}
