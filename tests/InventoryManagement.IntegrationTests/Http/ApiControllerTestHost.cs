using System.Net.Http.Headers;
using InventoryManagement.Api.Configuration;
using InventoryManagement.Api.Contracts.Common;
using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.IntegrationTests.Http.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace InventoryManagement.IntegrationTests.Http;

internal sealed class ApiControllerTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    internal Mock<ICategoryReadRepository> CategoryRead { get; } =
        new(MockBehavior.Strict);
    internal Mock<ICategoryWriteRepository> CategoryWrite { get; } =
        new(MockBehavior.Strict);
    internal Mock<IProductReadRepository> ProductRead { get; } =
        new(MockBehavior.Strict);
    internal Mock<IProductWriteRepository> ProductWrite { get; } =
        new(MockBehavior.Strict);
    internal Mock<IInventoryMovementWriteRepository> MovementWrite { get; } =
        new(MockBehavior.Strict);

    internal ApiControllerTestHost()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
        builder.WebHost.UseTestServer();
        RegisterServices(builder.Services);
        _app = builder.Build();
        _app.UseExceptionHandler();
        _app.UseStatusCodePages();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
    }

    private void RegisterServices(IServiceCollection services)
    {
        services.AddControllers()
            .AddApplicationPart(typeof(CreatedResourceResponse).Assembly);
        services.AddInventoryApplication();
        services.AddInventoryErrorHandling();
        services.AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName, _ => { });
        services.AddAuthorization();
        services.AddScoped<ICategoryReadRepository>(_ => CategoryRead.Object);
        services.AddScoped<ICategoryWriteRepository>(_ => CategoryWrite.Object);
        services.AddScoped<IProductReadRepository>(_ => ProductRead.Object);
        services.AddScoped<IProductWriteRepository>(_ => ProductWrite.Object);
        services.AddScoped<IInventoryMovementWriteRepository>(
            _ => MovementWrite.Object);
    }

    internal Task StartAsync()
    {
        return _app.StartAsync();
    }

    internal HttpClient CreateClient(bool authenticated = true)
    {
        var client = _app.GetTestClient();
        if (authenticated)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    TestAuthenticationHandler.SchemeName,
                    "authenticated");
        }

        return client;
    }

    internal void VerifyNoRepositoryCalls()
    {
        CategoryRead.VerifyNoOtherCalls();
        CategoryWrite.VerifyNoOtherCalls();
        ProductRead.VerifyNoOtherCalls();
        ProductWrite.VerifyNoOtherCalls();
        MovementWrite.VerifyNoOtherCalls();
    }

    public ValueTask DisposeAsync()
    {
        return _app.DisposeAsync();
    }
}
