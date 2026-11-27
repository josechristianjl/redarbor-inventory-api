using InventoryManagement.Api.Contracts.Common;
using InventoryManagement.Api.Contracts.InventoryMovements;
using InventoryManagement.Application.Abstractions.Messaging;
using InventoryManagement.Application.InventoryMovements.Commands.RegisterInventoryMovement;
using InventoryManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/inventory-movements")]
[ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status401Unauthorized,
    "application/problem+json")]
[ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status403Forbidden,
    "application/problem+json")]
[ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status500InternalServerError,
    "application/problem+json")]
public sealed class InventoryMovementsController : ControllerBase
{
    private readonly ICommandHandler<RegisterInventoryMovementCommand, int>
        _registerMovement;

    public InventoryMovementsController(
        ICommandHandler<RegisterInventoryMovementCommand, int>
            registerMovement)
    {
        ArgumentNullException.ThrowIfNull(registerMovement);
        _registerMovement = registerMovement;
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(CreatedResourceResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict,
        "application/problem+json")]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status415UnsupportedMediaType,
        "application/problem+json")]
    public async Task<ActionResult<CreatedResourceResponse>> Register(
        [FromBody] RegisterInventoryMovementRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterInventoryMovementCommand(
            request.ProductId,
            ParseMovementType(request.Type),
            request.Quantity);
        var id = await _registerMovement.HandleAsync(
            command, cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            new CreatedResourceResponse(id));
    }

    private static MovementType ParseMovementType(string? type)
    {
        return type switch
        {
            nameof(MovementType.Entry) => MovementType.Entry,
            nameof(MovementType.Exit) => MovementType.Exit,
            _ => (MovementType)0
        };
    }
}
