using System.Globalization;
using InventoryManagement.Application.Models;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace InventoryManagement.Api.Documentation;

public sealed class InventoryParameterFilter : IParameterFilter
{
    public void Apply(
        IOpenApiParameter parameter,
        ParameterFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(context);
        if (parameter is not OpenApiParameter concrete ||
            concrete.Schema is not OpenApiSchema schema)
        {
            return;
        }
        if (concrete.In == ParameterLocation.Path &&
            concrete.Name == "id")
        {
            schema.Minimum = "1";
        }
        else if (concrete.In == ParameterLocation.Query)
        {
            ConfigurePagination(concrete.Name, schema);
        }
    }

    private static void ConfigurePagination(
        string? name,
        OpenApiSchema schema)
    {
        if (name == "page")
        {
            schema.Minimum = "1";
        }
        else if (name == "pageSize")
        {
            schema.Minimum = "1";
            schema.Maximum = PageRequest.MaxPageSize.ToString(
                CultureInfo.InvariantCulture);
        }
    }
}
