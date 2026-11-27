using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace InventoryManagement.Api.Configuration;

public static class SwaggerServiceRegistration
{
    private const string SchemeName = "oauth2";
    private const string RequiredScope = "inventory.access";

    public static IServiceCollection AddInventorySwagger(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var issuer = GetRequiredValue(
            configuration, "KEYCLOAK_ISSUER");
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Inventory Management API",
                Version = "v1",
                Description = "REST API for managing categories, products and inventory movements."
            });
            ConfigureOAuth2(options, issuer);
        });

        return services;
    }

    public static WebApplication UseInventorySwagger(
        this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var clientId = GetRequiredValue(
            app.Configuration, "KEYCLOAK_SWAGGER_CLIENT_ID");
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(
                "/swagger/v1/swagger.json",
                "Inventory Management API v1");
            options.DocumentTitle = "Inventory Management API";
            options.OAuthClientId(clientId);
            options.OAuthAppName("Inventory Management");
            options.OAuthScopes(RequiredScope);
            options.OAuthUsePkce();
        });

        return app;
    }

    private static void ConfigureOAuth2(
        SwaggerGenOptions options,
        string issuer)
    {
        options.AddSecurityDefinition(
            SchemeName, CreateSecurityScheme(issuer));
        options.AddSecurityRequirement(document =>
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(
                    SchemeName, document)] = [RequiredScope]
            });
    }

    private static OpenApiSecurityScheme CreateSecurityScheme(
        string issuer)
    {
        return new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Authorization Code flow with PKCE.",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri(
                        $"{issuer}/protocol/openid-connect/auth"),
                    TokenUrl = new Uri(
                        $"{issuer}/protocol/openid-connect/token"),
                    Scopes = new Dictionary<string, string>
                    {
                        [RequiredScope] = "Access inventory management operations."
                    }
                }
            }
        };
    }

    private static string GetRequiredValue(
        IConfiguration configuration,
        string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"The configuration value '{key}' is required.");
        }

        return value;
    }
}
