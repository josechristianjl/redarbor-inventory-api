using FluentValidation;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.Categories.Commands.CreateCategory;
using InventoryManagement.Application.Categories.Commands.DeleteCategory;
using InventoryManagement.Application.Categories.Commands.UpdateCategory;
using InventoryManagement.Application.Categories.Queries.GetCategories;
using InventoryManagement.Application.Categories.Queries.GetCategoryById;
using InventoryManagement.Application.InventoryMovements.Commands.RegisterInventoryMovement;
using InventoryManagement.Application.Models;
using InventoryManagement.Application.Products.Commands.CreateProduct;
using InventoryManagement.Application.Products.Commands.DeleteProduct;
using InventoryManagement.Application.Products.Commands.UpdateProduct;
using InventoryManagement.Application.Products.Queries.GetProductById;
using InventoryManagement.Application.Products.Queries.GetProducts;
using InventoryManagement.Application.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagement.Api.Configuration;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddInventoryApplication(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IValidator<PageRequest>, PageRequestValidator>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        RegisterCategoryServices(services);
        RegisterProductServices(services);
        RegisterInventoryMovementServices(services);

        return services;
    }

    private static void RegisterCategoryServices(
        IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<GetCategoriesQuery, PagedResult<CategoryReadModel>>,
            GetCategoriesQueryHandler>();
        services.AddScoped<IQueryHandler<GetCategoryByIdQuery, CategoryReadModel>,
            GetCategoryByIdQueryHandler>();
        services.AddScoped<ICommandHandler<CreateCategoryCommand, int>,
            CreateCategoryCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateCategoryCommand>,
            UpdateCategoryCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteCategoryCommand>,
            DeleteCategoryCommandHandler>();
        services.AddScoped<IValidator<GetCategoryByIdQuery>,
            GetCategoryByIdQueryValidator>();
        services.AddScoped<IValidator<CreateCategoryCommand>,
            CreateCategoryCommandValidator>();
        services.AddScoped<IValidator<UpdateCategoryCommand>,
            UpdateCategoryCommandValidator>();
        services.AddScoped<IValidator<DeleteCategoryCommand>,
            DeleteCategoryCommandValidator>();
    }

    private static void RegisterProductServices(
        IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<GetProductsQuery, PagedResult<ProductReadModel>>,
            GetProductsQueryHandler>();
        services.AddScoped<IQueryHandler<GetProductByIdQuery, ProductReadModel>,
            GetProductByIdQueryHandler>();
        services.AddScoped<ICommandHandler<CreateProductCommand, int>,
            CreateProductCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateProductCommand>,
            UpdateProductCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteProductCommand>,
            DeleteProductCommandHandler>();
        services.AddScoped<IValidator<GetProductByIdQuery>,
            GetProductByIdQueryValidator>();
        services.AddScoped<IValidator<CreateProductCommand>,
            CreateProductCommandValidator>();
        services.AddScoped<IValidator<UpdateProductCommand>,
            UpdateProductCommandValidator>();
        services.AddScoped<IValidator<DeleteProductCommand>,
            DeleteProductCommandValidator>();
    }

    private static void RegisterInventoryMovementServices(
        IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<RegisterInventoryMovementCommand, int>,
            RegisterInventoryMovementCommandHandler>();
        services.AddScoped<IValidator<RegisterInventoryMovementCommand>,
            RegisterInventoryMovementCommandValidator>();
    }
}
