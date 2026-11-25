namespace InventoryManagement.Domain.Exceptions;

public enum DomainRuleCode
{
    InactiveCategory = 1,
    InactiveProduct = 2,
    InsufficientStock = 3,
    StockLimitExceeded = 4,
    ProductHasStock = 5
}