using System.Text.Json.Serialization;

namespace InventoryManagement.Api.Contracts.Categories;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CategoryWriteRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
}
