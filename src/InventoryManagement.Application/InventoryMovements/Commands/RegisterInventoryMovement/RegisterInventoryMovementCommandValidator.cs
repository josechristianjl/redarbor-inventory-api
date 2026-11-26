using FluentValidation;
using InventoryManagement.Application.Products.Validation;
using InventoryManagement.Domain.Enums;

namespace InventoryManagement.Application.InventoryMovements.Commands.RegisterInventoryMovement;

public sealed class RegisterInventoryMovementCommandValidator
    : AbstractValidator<RegisterInventoryMovementCommand>
{
    public RegisterInventoryMovementCommandValidator()
    {
        RuleFor(command => command.ProductId)
            .ValidProductId();
        RuleFor(command => command.Type)
            .Must(type => type is MovementType.Entry or MovementType.Exit)
            .WithMessage("Movement type must be Entry or Exit.");
        RuleFor(command => command.Quantity)
            .GreaterThan(0)
            .WithMessage("Movement quantity must be greater than zero.");
    }
}
