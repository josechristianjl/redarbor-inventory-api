namespace InventoryManagement.Application.Exceptions;

public sealed class PersistenceRuleException : Exception
{
    public PersistenceRuleCode Code { get; }

    public PersistenceRuleException(
        PersistenceRuleCode code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }
}
