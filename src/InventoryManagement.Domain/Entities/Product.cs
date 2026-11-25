using InventoryManagement.Domain.Exceptions;
using InventoryManagement.Domain.Parameters;
using InventoryManagement.Domain.Validation;

namespace InventoryManagement.Domain.Entities;

public class Product
{
    public const int MaxSkuLength = 50;
    public const int MaxNameLength = 150;
    public const int MaxDescriptionLength = 500;
    public int Id { get; private set; }
    public string SKU { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int CategoryId { get; private set; }
    public int Stock { get; private set; }
    public bool IsActive { get; private set; }

    private Product()
    {
    }
    public Product(ProductDetails details)
    {
        ApplyDetails(details);
        Stock = 0;
        IsActive = true;
    }

    public void UpdateDetails(ProductDetails details)
    {
        if (!IsActive)
        {
            throw new DomainRuleException(
                DomainRuleCode.InactiveProduct,
                "An inactive product cannot be updated.");
        }
        ApplyDetails(details);
    }

    public void Deactivate()
    {
        if (Stock > 0)
        {
            throw new DomainRuleException(
                DomainRuleCode.ProductHasStock,
                "A product with available stock cannot be deactivated.");
        }
        IsActive = false;
    }

    public void RegisterEntry(int quantity)
    {
        EnsureActiveForMovement();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (quantity > int.MaxValue - Stock)
        {
            throw new DomainRuleException(
                DomainRuleCode.StockLimitExceeded,
                "The inventory stock limit would be exceeded.");
        }
        Stock += quantity;
    }

    public void RegisterExit(int quantity)
    {
        EnsureActiveForMovement();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (quantity > Stock)
        {
            throw new DomainRuleException(
                DomainRuleCode.InsufficientStock,
                "Insufficient inventory stock.");
        }
        Stock -= quantity;
    }

    private void ApplyDetails(ProductDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        var (sku, name, categoryId, description) = details;
        string normalizedSku = TextValidation.NormalizeRequired(
            sku, MaxSkuLength, nameof(sku));
        string normalizedName = TextValidation.NormalizeRequired(
            name, MaxNameLength, nameof(name));
        int validatedCategoryId = ValidateCategoryId(categoryId);
        string? normalizedDescription = TextValidation.NormalizeOptional(
            description, MaxDescriptionLength, nameof(description));
        SKU = normalizedSku;
        Name = normalizedName;
        CategoryId = validatedCategoryId;
        Description = normalizedDescription;
    }

    private void EnsureActiveForMovement()
    {
        if (!IsActive)
        {
            throw new DomainRuleException(
                DomainRuleCode.InactiveProduct,
                "An inactive product cannot register inventory movements.");
        }
    }

    private static int ValidateCategoryId(int categoryId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);

        return categoryId;
    }
}