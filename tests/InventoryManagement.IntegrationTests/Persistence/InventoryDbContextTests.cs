using InventoryManagement.Domain.Entities;
using InventoryManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.IntegrationTests.Persistence;

public class InventoryDbContextTests
{
    [Fact]
    public void CategoryMapping_ShouldMatchExpectedSchema()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(Category));
        Assert.NotNull(entity);
        Assert.Equal("Categories", entity.GetTableName());
        Assert.Equal("dbo", entity.GetSchema());
        var name = entity.FindProperty(nameof(Category.Name));
        Assert.NotNull(name);
        Assert.Equal(150, name.GetMaxLength());
        Assert.False(name.IsNullable);
        var description = entity.FindProperty(nameof(Category.Description));
        Assert.NotNull(description);
        Assert.Equal(500, description.GetMaxLength());
        Assert.True(description.IsNullable);
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UQ_Categories_Name");
    }

    [Fact]
    public void ProductMapping_ShouldDefineCategoryRelationship()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(Product));
        Assert.NotNull(entity);
        Assert.Equal("Products", entity.GetTableName());
        Assert.Equal("dbo", entity.GetSchema());
        var sku = entity.FindProperty(nameof(Product.SKU));
        Assert.NotNull(sku);
        Assert.Equal(50, sku.GetMaxLength());
        Assert.False(sku.IsNullable);
        var foreignKey = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(Category), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(Product.CategoryId),
            Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior);
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "UQ_Products_SKU");
        Assert.Contains(entity.GetIndexes(), index =>
            index.GetDatabaseName() == "IX_Products_CategoryId_IsActive");
    }

    [Fact]
    public void InventoryMovementMapping_ShouldMatchExpectedSchema()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(InventoryMovement));
        Assert.NotNull(entity);
        Assert.Equal("InventoryMovements", entity.GetTableName());
        Assert.Equal("dbo", entity.GetSchema());
        var type = entity.FindProperty(nameof(InventoryMovement.Type));
        Assert.NotNull(type);
        Assert.Equal("tinyint", type.GetColumnType());
        var createdAt = entity.FindProperty(nameof(InventoryMovement.CreatedAt));
        Assert.NotNull(createdAt);
        Assert.Equal("datetime2(7)", createdAt.GetColumnType());
        var foreignKey = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(Product), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(InventoryMovement.ProductId),
            Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void CreatedAtConversion_ShouldRestoreUtcKind()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(InventoryMovement));
        Assert.NotNull(entity);
        var property = entity.FindProperty(nameof(InventoryMovement.CreatedAt));
        Assert.NotNull(property);
        var converter = property.GetValueConverter();
        Assert.NotNull(converter);
        var storedValue = new DateTime(
            2026, 11, 25, 18, 0, 0, DateTimeKind.Unspecified);
        var result = Assert.IsType<DateTime>(
            converter.ConvertFromProvider(storedValue));
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(storedValue.Ticks, result.Ticks);
    }

    private static InventoryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=InventoryManagement;Integrated Security=True;")
            .Options;

        return new InventoryDbContext(options);
    }
}
