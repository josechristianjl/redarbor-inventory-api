using InventoryManagement.Api.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InventoryManagement.Api.Configuration;

public static class HealthCheckServiceRegistration
{
    private const string ReadinessTag = "ready";

    public static IServiceCollection AddInventoryHealthChecks(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>(
                "sqlserver",
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadinessTag],
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    public static WebApplication MapInventoryHealthChecks(
        this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration =>
                registration.Tags.Contains(ReadinessTag)
        }).AllowAnonymous();

        return app;
    }
}
