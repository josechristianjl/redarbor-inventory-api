using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Exceptions;

namespace InventoryManagement.Domain.Tests.Entities;

public class CategoryTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldCreateActiveCategory()
    {
        var category = new Category("Electronics", "Electronic products");
        Assert.Equal("Electronics", category.Name);
        Assert.Equal("Electronic products", category.Description);
        Assert.True(category.IsActive);
        Assert.Equal(0, category.Id);
    }

    [Fact]
    public void Constructor_WithoutDescription_ShouldCreateCategory()
    {
        var category = new Category("Electronics");
        Assert.Null(category.Description);
        Assert.True(category.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ShouldThrowException(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Category(name!));
    }

    [Fact]
    public void Constructor_WithNameExceedingMaximumLength_ShouldThrowException()
    {
        var name = new string('A', 151);
        Assert.Throws<ArgumentException>(() => new Category(name));
    }

    [Fact]
    public void Constructor_WithDescriptionExceedingMaximumLength_ShouldThrowException()
    {
        var description = new string('A', 501);
        Assert.Throws<ArgumentException>(() => new Category("Electronics", description));
    }

    [Fact]
    public void Constructor_WithWhitespace_ShouldTrimValues()
    {
        var category = new Category("  Electronics  ", "  Electronic products  ");
        Assert.Equal("Electronics", category.Name);
        Assert.Equal("Electronic products", category.Description);
    }

    [Fact]
    public void Constructor_WithMaximumAllowedLengths_ShouldCreateCategory()
    {
        var name = new string('A', 150);
        var description = new string('B', 500);
        var category = new Category(name, description);
        Assert.Equal(150, category.Name.Length);
        Assert.Equal(description, category.Description);
    }

    [Fact]
    public void Constructor_WithWhitespaceDescription_ShouldSetDescriptionToNull()
    {
        var category = new Category("Electronics", "   ");
        Assert.Null(category.Description);
    }

    [Fact]
    public void UpdateDetails_WithValidData_ShouldUpdateCategory()
    {
        var category = new Category("Electronics", "Electronic products");
        category.UpdateDetails("  Computers  ", "  Computer products  ");
        Assert.Equal("Computers", category.Name);
        Assert.Equal("Computer products", category.Description);
        Assert.True(category.IsActive);
    }

    [Fact]
    public void UpdateDetails_WithNullDescription_ShouldClearDescription()
    {
        var category = new Category("Electronics", "Electronic products");
        category.UpdateDetails("Computers", null);
        Assert.Equal("Computers", category.Name);
        Assert.Null(category.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDetails_WithInvalidName_ShouldPreserveOriginalData(string? name)
    {
        var category = new Category("Electronics", "Electronic products");
        Assert.ThrowsAny<ArgumentException>(() =>
            category.UpdateDetails(name!, "New description"));
        Assert.Equal("Electronics", category.Name);
        Assert.Equal("Electronic products", category.Description);
    }

    [Fact]
    public void UpdateDetails_WithNameExceedingMaximumLength_ShouldThrowException()
    {
        var category = new Category("Electronics");
        var name = new string('A', 151);
        Assert.Throws<ArgumentException>(() =>
            category.UpdateDetails(name, null));
    }

    [Fact]
    public void UpdateDetails_WithInvalidDescription_ShouldPreserveOriginalData()
    {
        var category = new Category("Electronics", "Electronic products");
        var description = new string('A', 501);
        Assert.Throws<ArgumentException>(() =>
            category.UpdateDetails("Computers", description));
        Assert.Equal("Electronics", category.Name);
        Assert.Equal("Electronic products", category.Description);
    }

    [Fact]
    public void UpdateDetails_WhenCategoryIsInactive_ShouldThrowException()
    {
        var category = new Category("Electronics");
        category.Deactivate();
        var exception = Assert.Throws<DomainRuleException>(() =>
            category.UpdateDetails("Computers", null));
        Assert.Equal(DomainRuleCode.InactiveCategory, exception.Code);
        Assert.Equal("Electronics", category.Name);
    }

    [Fact]
    public void Deactivate_WhenCategoryIsActive_ShouldSetIsActiveToFalse()
    {
        var category = new Category("Electronics", "Electronic products");
        category.Deactivate();
        Assert.False(category.IsActive);
        Assert.Equal("Electronics", category.Name);
        Assert.Equal("Electronic products", category.Description);
    }

    [Fact]
    public void Deactivate_WhenCategoryIsAlreadyInactive_ShouldRemainInactive()
    {
        var category = new Category("Electronics");
        category.Deactivate();
        category.Deactivate();
        Assert.False(category.IsActive);
    }
}
