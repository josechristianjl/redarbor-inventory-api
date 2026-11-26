namespace InventoryManagement.Application.Abstractions.Messaging;

public interface ICommandHandler<in TCommand>
{
    Task HandleAsync(
        TCommand command,
        CancellationToken cancellationToken);
}
