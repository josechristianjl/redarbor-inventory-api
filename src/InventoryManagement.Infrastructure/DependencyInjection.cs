using InventoryManagement.Application.Abstractions.Persistence;
using InventoryManagement.Infrastructure.Persistence;
using InventoryManagement.Infrastructure.Persistence.Connections;
using InventoryManagement.Infrastructure.Persistence.Repositories.Read;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using InventoryManagement.Infrastructure.Persistence.Repositories.Write;

namespace InventoryManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        var connectionString =
            SqlServerConnectionString.Create(configuration);
        services.AddSingleton(
            new SqlConnectionFactory(connectionString));
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<ICategoryReadRepository, CategoryReadRepository>();
        services.AddScoped<IProductReadRepository, ProductReadRepository>();
        services.AddScoped<ICategoryWriteRepository, CategoryWriteRepository>();
        services.AddScoped<IProductWriteRepository, ProductWriteRepository>();
        services.AddScoped<IInventoryMovementWriteRepository, InventoryMovementWriteRepository>();

        return services;
    }
}
