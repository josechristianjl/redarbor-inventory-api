using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Domain.Entities;

public class InventoryMovement
{
    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public MovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private InventoryMovement()
    {
    }
    public InventoryMovement(
        int productId,
        MovementType type,
        int quantity,
        DateTime createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ValidateMovementType(type);
        ValidateCreatedAt(createdAt);
        ProductId = productId;
        Type = type;
        Quantity = quantity;
        CreatedAt = createdAt;
    }

    private static void ValidateMovementType(MovementType type)
    {
        if (type is not MovementType.Entry and not MovementType.Exit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "The inventory movement type is invalid.");
        }
    }

    private static void ValidateCreatedAt(DateTime createdAt)
    {
        if (createdAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "The movement creation date must be in UTC.",
                nameof(createdAt));
        }
    }
}