using InventoryManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", "dbo");
        builder.HasKey(category => category.Id)
            .HasName("PK_Categories");
        builder.Property(category => category.Id)
            .UseIdentityColumn();
        builder.Property(category => category.Name)
            .HasMaxLength(Category.MaxNameLength)
            .UseCollation("Latin1_General_100_CI_AS_SC")
            .IsRequired();
        builder.Property(category => category.Description)
            .HasMaxLength(Category.MaxDescriptionLength)
            .IsRequired(false);
        builder.Property(category => category.IsActive)
            .HasDefaultValue(true)
            .IsRequired();
        builder.HasIndex(category => category.Name)
            .IsUnique()
            .HasDatabaseName("UQ_Categories_Name");
    }
}
