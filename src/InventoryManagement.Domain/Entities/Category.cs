using InventoryManagement.Domain.Validation;
using InventoryManagement.Domain.Exceptions;

namespace InventoryManagement.Domain.Entities;

public class Category
{
    public const int MaxNameLength = 150;
    public const int MaxDescriptionLength = 500;
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    private Category()
    {
    }

    public Category(string name, string? description = null)
    {
        Name = TextValidation.NormalizeRequired(
            name, MaxNameLength, nameof(name));
        Description = TextValidation.NormalizeOptional(
            description, MaxDescriptionLength, nameof(description));
        IsActive = true;
    }

    public void UpdateDetails(string name, string? description)
    {
        if (!IsActive)
        {
            throw new DomainRuleException(
                DomainRuleCode.InactiveCategory,
                "An inactive category cannot be updated.");
        }
        string normalizedName = TextValidation.NormalizeRequired(
            name, MaxNameLength, nameof(name));
        string? normalizedDescription = TextValidation.NormalizeOptional(
            description, MaxDescriptionLength, nameof(description));
        Name = normalizedName;
        Description = normalizedDescription;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static Category Restore(
    int id,
    string name,
    string? description,
    bool isActive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return new Category(name, description)
        {
            Id = id,
            IsActive = isActive
        };
    }
}
