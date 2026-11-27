using InventoryManagement.Api.ErrorHandling;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Configuration;

public static class ErrorHandlingServiceRegistration
{
    public static IServiceCollection AddInventoryErrorHandling(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.Configure<ExceptionHandlerOptions>(options =>
        {
            options.SuppressDiagnosticsCallback = context =>
                context.HttpContext.Response.StatusCode < 500;
        });
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressMapClientErrors = true;
            options.InvalidModelStateResponseFactory =
                HttpErrorResponses.CreateInvalidModelStateResponse;
        });
        services.Configure<StatusCodePagesOptions>(options =>
        {
            options.HandleAsync =
                HttpErrorResponses.WriteStatusCodeAsync;
        });

        return services;
    }
}
