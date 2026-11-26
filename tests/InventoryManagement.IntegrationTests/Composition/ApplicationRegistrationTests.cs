using FluentValidation;
using InventoryManagement.Api.Configuration;
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
using InventoryManagement.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagement.IntegrationTests.Composition;

public class ApplicationRegistrationTests
{
    [Theory]
    [InlineData(
        typeof(IQueryHandler<GetCategoriesQuery, PagedResult<CategoryReadModel>>),
        typeof(GetCategoriesQueryHandler))]
    [InlineData(
        typeof(IQueryHandler<GetCategoryByIdQuery, CategoryReadModel>),
        typeof(GetCategoryByIdQueryHandler))]
    [InlineData(
        typeof(ICommandHandler<CreateCategoryCommand, int>),
        typeof(CreateCategoryCommandHandler))]
    [InlineData(
        typeof(ICommandHandler<UpdateCategoryCommand>),
        typeof(UpdateCategoryCommandHandler))]
    [InlineData(
        typeof(ICommandHandler<DeleteCategoryCommand>),
        typeof(DeleteCategoryCommandHandler))]
    [InlineData(
        typeof(IQueryHandler<GetProductsQuery, PagedResult<ProductReadModel>>),
        typeof(GetProductsQueryHandler))]
    [InlineData(
        typeof(IQueryHandler<GetProductByIdQuery, ProductReadModel>),
        typeof(GetProductByIdQueryHandler))]
    [InlineData(
        typeof(ICommandHandler<CreateProductCommand, int>),
        typeof(CreateProductCommandHandler))]
    [InlineData(
        typeof(ICommandHandler<UpdateProductCommand>),
        typeof(UpdateProductCommandHandler))]
    [InlineData(
        typeof(ICommandHandler<DeleteProductCommand>),
        typeof(DeleteProductCommandHandler))]
    [InlineData(
        typeof(ICommandHandler<RegisterInventoryMovementCommand, int>),
        typeof(RegisterInventoryMovementCommandHandler))]
    public void Registration_ShouldResolveScopedHandler(
        Type serviceType,
        Type implementationType)
    {
        AssertScopedRegistration(serviceType, implementationType);
    }

    [Theory]
    [InlineData(
        typeof(IValidator<PageRequest>),
        typeof(PageRequestValidator))]
    [InlineData(
        typeof(IValidator<GetCategoryByIdQuery>),
        typeof(GetCategoryByIdQueryValidator))]
    [InlineData(
        typeof(IValidator<CreateCategoryCommand>),
        typeof(CreateCategoryCommandValidator))]
    [InlineData(
        typeof(IValidator<UpdateCategoryCommand>),
        typeof(UpdateCategoryCommandValidator))]
    [InlineData(
        typeof(IValidator<DeleteCategoryCommand>),
        typeof(DeleteCategoryCommandValidator))]
    [InlineData(
        typeof(IValidator<GetProductByIdQuery>),
        typeof(GetProductByIdQueryValidator))]
    [InlineData(
        typeof(IValidator<CreateProductCommand>),
        typeof(CreateProductCommandValidator))]
    [InlineData(
        typeof(IValidator<UpdateProductCommand>),
        typeof(UpdateProductCommandValidator))]
    [InlineData(
        typeof(IValidator<DeleteProductCommand>),
        typeof(DeleteProductCommandValidator))]
    [InlineData(
        typeof(IValidator<RegisterInventoryMovementCommand>),
        typeof(RegisterInventoryMovementCommandValidator))]
    public void Registration_ShouldResolveScopedValidator(
        Type serviceType,
        Type implementationType)
    {
        AssertScopedRegistration(serviceType, implementationType);
    }

    [Fact]
    public void Registration_ShouldResolveSystemTimeProviderAsSingleton()
    {
        using var provider = CreateServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider
            .GetRequiredService<TimeProvider>();
        var second = secondScope.ServiceProvider
            .GetRequiredService<TimeProvider>();
        Assert.Same(TimeProvider.System, first);
        Assert.Same(first, second);
        Assert.Same(first,
            Assert.Single(provider.GetServices<TimeProvider>()));
    }

    private static void AssertScopedRegistration(
        Type serviceType,
        Type implementationType)
    {
        using var provider = CreateServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider
            .GetRequiredService(serviceType);
        var repeated = firstScope.ServiceProvider
            .GetRequiredService(serviceType);
        var second = secondScope.ServiceProvider
            .GetRequiredService(serviceType);
        Assert.IsType(implementationType, first);
        Assert.Same(first, repeated);
        Assert.NotSame(first, second);
        Assert.Same(first, Assert.Single(firstScope.ServiceProvider
            .GetServices(serviceType)));
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddInventoryApplication();
        services.AddInventoryPersistence(CreateConfiguration());

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private static IConfiguration CreateConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["SQL_SERVER_HOST"] = "127.0.0.1",
            ["SQL_SERVER_PORT"] = "14330",
            ["SQL_SERVER_DATABASE"] = "InventoryManagement",
            ["SQL_SERVER_USER"] = "inventory_app",
            ["SQL_SERVER_PASSWORD"] = "TestOnly!Example123"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
