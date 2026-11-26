using InventoryManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Infrastructure.Persistence.Configurations;

internal sealed class InventoryMovementConfiguration
    : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements", "dbo");
        builder.HasKey(movement => movement.Id)
            .HasName("PK_InventoryMovements");
        builder.Property(movement => movement.Id).UseIdentityColumn();
        builder.Property(movement => movement.ProductId).IsRequired();
        builder.Property(movement => movement.Type)
            .HasConversion<byte>()
            .HasColumnType("tinyint")
            .IsRequired();
        builder.Property(movement => movement.Quantity).IsRequired();
        builder.Property(movement => movement.CreatedAt)
            .HasColumnType("datetime2(7)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .HasConversion(
                value => value,
                value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
            .IsRequired();
        builder.HasIndex(movement => new { movement.ProductId, movement.CreatedAt })
            .HasDatabaseName("IX_InventoryMovements_ProductId_CreatedAt");
        ConfigureRelationships(builder);
    }
    private static void ConfigureRelationships(
        EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(movement => movement.ProductId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_InventoryMovements_Products");
    }
}
