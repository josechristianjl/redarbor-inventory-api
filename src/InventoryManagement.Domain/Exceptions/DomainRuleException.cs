namespace InventoryManagement.Domain.Exceptions;

public sealed class DomainRuleException : Exception
{
    public DomainRuleCode Code { get; }
    public DomainRuleException(DomainRuleCode code, string message)
        : base(message)
    {
        Code = code;
    }
}