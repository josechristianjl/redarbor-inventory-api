using InventoryManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", "dbo");
        builder.HasKey(product => product.Id)
            .HasName("PK_Products");
        builder.Property(product => product.Id).UseIdentityColumn();
        builder.Property(product => product.CategoryId).IsRequired();
        builder.Property(product => product.SKU)
            .HasMaxLength(Product.MaxSkuLength)
            .UseCollation("Latin1_General_100_CI_AS_SC")
            .IsRequired();
        builder.Property(product => product.Name)
            .HasMaxLength(Product.MaxNameLength).IsRequired();
        builder.Property(product => product.Description)
            .HasMaxLength(Product.MaxDescriptionLength).IsRequired(false);
        builder.Property(product => product.Stock).HasDefaultValue(0).IsRequired();
        builder.Property(product => product.IsActive).HasDefaultValue(true).IsRequired();
        builder.HasIndex(product => product.SKU)
            .IsUnique().HasDatabaseName("UQ_Products_SKU");
        builder.HasIndex(product => new { product.CategoryId, product.IsActive })
            .HasDatabaseName("IX_Products_CategoryId_IsActive");
        ConfigureRelationships(builder);
    }
    private static void ConfigureRelationships(EntityTypeBuilder<Product> builder)
    {
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Products_Categories");
    }
}
