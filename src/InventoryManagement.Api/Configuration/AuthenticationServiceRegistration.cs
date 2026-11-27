using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace InventoryManagement.Api.Configuration;

public static class AuthenticationServiceRegistration
{
    private const string RequiredScope = "inventory.access";

    public static IServiceCollection AddInventoryAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        var allowHttp = environment.IsDevelopment();
        var authority = GetRequiredUrl(
            configuration, "KEYCLOAK_AUTHORITY", allowHttp);
        var issuer = GetRequiredUrl(
            configuration, "KEYCLOAK_ISSUER", allowHttp);
        var audience = GetRequiredValue(
            configuration, "KEYCLOAK_AUDIENCE");
        services.AddAuthentication(
                JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = !allowHttp;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.SaveToken = false;
                options.TokenValidationParameters =
                    CreateTokenValidationParameters(issuer, audience);
            });
        RegisterAuthorizationPolicy(services);

        return services;
    }

    private static TokenValidationParameters CreateTokenValidationParameters(
        string issuer,
        string audience)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            IssuerValidator = (tokenIssuer, _, _) =>
                ValidateExpectedIssuer(tokenIssuer, issuer),
            ValidateAudience = true,
            ValidAudience = audience,
            IgnoreTrailingSlashWhenValidatingAudience = false,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }

    private static string ValidateExpectedIssuer(
        string tokenIssuer,
        string expectedIssuer)
    {
        if (!string.Equals(
                tokenIssuer, expectedIssuer, StringComparison.Ordinal))
        {
            throw new SecurityTokenInvalidIssuerException(
                "The token issuer is invalid.");
        }

        return tokenIssuer;
    }

    private static void RegisterAuthorizationPolicy(
        IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                    JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireAssertion(HasRequiredScope)
                .Build();
        });
    }

    private static bool HasRequiredScope(
        AuthorizationHandlerContext context)
    {
        return context.User.FindAll("scope")
            .Any(claim => claim.Value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains(RequiredScope, StringComparer.Ordinal));
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

    private static string GetRequiredUrl(
        IConfiguration configuration,
        string key,
        bool allowHttp)
    {
        var value = GetRequiredValue(configuration, key);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
                (!allowHttp || uri.Scheme != Uri.UriSchemeHttp)))
        {
            throw new InvalidOperationException(
                $"The configuration value '{key}' contains an invalid URL.");
        }

        return value;
    }
}
