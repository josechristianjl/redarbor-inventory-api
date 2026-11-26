namespace InventoryManagement.Application.Exceptions;

public enum PersistenceRuleCode
{
    CategoryNotFound = 1,
    ProductNotFound = 2,
    DuplicateCategoryName = 3,
    DuplicateSku = 4,
    CategoryHasActiveProducts = 5,
    ConcurrentProductChange = 6
}
