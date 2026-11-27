using System.Text.Json.Nodes;
using InventoryManagement.Api.Contracts.Categories;
using InventoryManagement.Api.Contracts.InventoryMovements;
using InventoryManagement.Api.Contracts.Products;
using InventoryManagement.Domain.Enums;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace InventoryManagement.Api.Documentation;

public sealed class InventoryRequestSchemaFilter : ISchemaFilter
{
    public void Apply(
        IOpenApiSchema schema,
        SchemaFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);
        if (schema is not OpenApiSchema concrete)
        {
            return;
        }
        if (context.Type == typeof(CategoryWriteRequest))
        {
            ConfigureCategory(concrete);
        }
        else if (context.Type == typeof(ProductWriteRequest))
        {
            ConfigureProduct(concrete);
        }
        else if (context.Type == typeof(RegisterInventoryMovementRequest))
        {
            ConfigureMovement(concrete);
        }
    }

    private static void ConfigureCategory(OpenApiSchema schema)
    {
        ConfigureRequiredProperties(schema, ["name"]);
        ConfigureRequiredText(
            GetProperty(schema, "name"), 150);
        ConfigureDescription(
            GetProperty(schema, "description"));
    }

    private static void ConfigureProduct(OpenApiSchema schema)
    {
        ConfigureRequiredProperties(
            schema, ["sku", "name", "categoryId"]);
        ConfigureRequiredText(
            GetProperty(schema, "sku"), 50);
        ConfigureRequiredText(
            GetProperty(schema, "name"), 150);
        ConfigurePositiveInteger(
            GetProperty(schema, "categoryId"));
        ConfigureDescription(
            GetProperty(schema, "description"));
    }

    private static void ConfigureMovement(OpenApiSchema schema)
    {
        ConfigureRequiredProperties(
            schema, ["productId", "type", "quantity"]);
        ConfigurePositiveInteger(
            GetProperty(schema, "productId"));
        ConfigurePositiveInteger(
            GetProperty(schema, "quantity"));
        var movementType = GetProperty(schema, "type");
        movementType.Type = JsonSchemaType.String;
        movementType.Description =
            "Case-sensitive inventory movement type.";
        movementType.Enum =
        [
            JsonValue.Create(nameof(MovementType.Entry)),
            JsonValue.Create(nameof(MovementType.Exit))
        ];
    }

    private static void ConfigureRequiredProperties(
        OpenApiSchema schema,
        IEnumerable<string> propertyNames)
    {
        schema.Required = new HashSet<string>(
            propertyNames, StringComparer.Ordinal);
        schema.AdditionalPropertiesAllowed = false;
    }

    private static void ConfigureRequiredText(
        OpenApiSchema property,
        int maximumLength)
    {
        property.Type = JsonSchemaType.String;
        property.MinLength = 1;
        property.Description =
            $"Required, non-blank. Maximum {maximumLength} characters after trimming.";
    }

    private static void ConfigureDescription(
        OpenApiSchema property)
    {
        property.Description =
            "Optional. Maximum 500 characters after trimming.";
    }

    private static void ConfigurePositiveInteger(
        OpenApiSchema property)
    {
        property.Type = JsonSchemaType.Integer;
        property.Minimum = "1";
    }

    private static OpenApiSchema GetProperty(
        OpenApiSchema schema,
        string name)
    {
        return schema.Properties?[name] as OpenApiSchema
            ?? throw new InvalidOperationException(
                $"OpenAPI property '{name}' was not found.");
    }
}
