using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Application.InventoryMovements.Commands.RegisterInventoryMovement;

public sealed record RegisterInventoryMovementCommand(
    int ProductId,
    MovementType Type,
    int Quantity);
