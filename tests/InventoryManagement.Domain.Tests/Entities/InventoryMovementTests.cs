using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Domain.Tests.Entities;

public class InventoryMovementTests
{
    [Theory]
    [InlineData(MovementType.Entry, 1)]
    [InlineData(MovementType.Exit, 2)]
    public void Constructor_WithValidData_ShouldCreateMovement(
        MovementType type,
        int expectedTypeValue)
    {
        var createdAt = new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        var movement = new InventoryMovement(1, type, 10, createdAt);
        Assert.Equal(0, movement.Id);
        Assert.Equal(1, movement.ProductId);
        Assert.Equal(type, movement.Type);
        Assert.Equal(expectedTypeValue, (int)movement.Type);
        Assert.Equal(10, movement.Quantity);
        Assert.Equal(createdAt, movement.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, movement.CreatedAt.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidProductId_ShouldThrowException(int productId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InventoryMovement(
                productId,
                MovementType.Entry,
                10,
                DateTime.UtcNow));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_WithInvalidQuantity_ShouldThrowException(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InventoryMovement(
                1,
                MovementType.Entry,
                quantity,
                DateTime.UtcNow));
    }

    [Theory]
    [InlineData((MovementType)0)]
    [InlineData((MovementType)3)]
    [InlineData((MovementType)255)]
    public void Constructor_WithInvalidMovementType_ShouldThrowException(
        MovementType type)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InventoryMovement(
                1,
                type,
                10,
                DateTime.UtcNow));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Constructor_WithNonUtcDate_ShouldThrowException(DateTimeKind kind)
    {
        var createdAt = new DateTime(2026, 11, 25, 12, 0, 0, kind);
        Assert.Throws<ArgumentException>(() =>
            new InventoryMovement(
                1,
                MovementType.Entry,
                10,
                createdAt));
    }

    [Fact]
    public void Constructor_WithMaximumQuantity_ShouldCreateMovement()
    {
        var movement = new InventoryMovement(
            1,
            MovementType.Entry,
            int.MaxValue,
            DateTime.UtcNow);
        Assert.Equal(int.MaxValue, movement.Quantity);
    }
}
