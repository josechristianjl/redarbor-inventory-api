using InventoryManagement.Domain.Entities;
using InventoryManagement.Domain.Exceptions;
using InventoryManagement.Domain.Parameters;

namespace InventoryManagement.Domain.Tests.Entities;

public class ProductTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldCreateActiveProduct()
    {
        var product = new Product(new ProductDetails("SKU-001", "Laptop", 1, "Electronic device"));
        Assert.Equal("SKU-001", product.SKU);
        Assert.Equal("Laptop", product.Name);
        Assert.Equal("Electronic device", product.Description);
        Assert.Equal(1, product.CategoryId);
        Assert.Equal(0, product.Stock);
        Assert.Equal(0, product.Id);
        Assert.True(product.IsActive);
    }

    [Fact]
    public void Constructor_WithoutDescription_ShouldCreateProduct()
    {
        var product = new Product(new ProductDetails("SKU-001", "Laptop", 1));
        Assert.Null(product.Description);
        Assert.True(product.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidSku_ShouldThrowException(string? sku)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new Product(new ProductDetails(sku!, "Laptop", 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ShouldThrowException(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new Product(new ProductDetails("SKU-001", name!, 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidCategoryId_ShouldThrowException(int categoryId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Product(new ProductDetails("SKU-001", "Laptop", categoryId)));
    }

    [Fact]
    public void Constructor_WithSkuExceedingMaximumLength_ShouldThrowException()
    {
        var sku = new string('A', 51);
        Assert.Throws<ArgumentException>(() =>
            new Product(new ProductDetails(sku, "Laptop", 1)));
    }

    [Fact]
    public void Constructor_WithNameExceedingMaximumLength_ShouldThrowException()
    {
        var name = new string('A', 151);
        Assert.Throws<ArgumentException>(() =>
            new Product(new ProductDetails("SKU-001", name, 1)));
    }

    [Fact]
    public void Constructor_WithDescriptionExceedingMaximumLength_ShouldThrowException()
    {
        var description = new string('A', 501);
        Assert.Throws<ArgumentException>(() =>
            new Product(new ProductDetails("SKU-001", "Laptop", 1, description)));
    }

    [Fact]
    public void Constructor_WithWhitespace_ShouldTrimValues()
    {
        var product = new Product(new ProductDetails("  SKU-001  ", "  Laptop  ", 1, "  Device  "));
        Assert.Equal("SKU-001", product.SKU);
        Assert.Equal("Laptop", product.Name);
        Assert.Equal("Device", product.Description);
    }

    [Fact]
    public void Constructor_WithMaximumAllowedLengths_ShouldCreateProduct()
    {
        var sku = new string('A', 50);
        var name = new string('B', 150);
        var description = new string('C', 500);
        var product = new Product(new ProductDetails(sku, name, 1, description));
        Assert.Equal(sku, product.SKU);
        Assert.Equal(name, product.Name);
        Assert.Equal(description, product.Description);
    }

    [Fact]
    public void Constructor_WithWhitespaceDescription_ShouldSetDescriptionToNull()
    {
        var product = new Product(new ProductDetails("SKU-001", "Laptop", 1, "   "));
        Assert.Null(product.Description);
    }

    [Fact]
    public void UpdateDetails_WithValidData_ShouldUpdateProduct()
    {
        var product = new Product(new ProductDetails("SKU-001", "Laptop", 1, "Device"));
        product.UpdateDetails(new ProductDetails("  SKU-002  ", "  Monitor  ", 2, "  Display  "));
        Assert.Equal("SKU-002", product.SKU);
        Assert.Equal("Monitor", product.Name);
        Assert.Equal("Display", product.Description);
        Assert.Equal(2, product.CategoryId);
        Assert.Equal(0, product.Stock);
        Assert.Equal(0, product.Id);
        Assert.True(product.IsActive);
    }

    [Fact]
    public void UpdateDetails_WithNullDescription_ShouldClearDescription()
    {
        var product = new Product(new ProductDetails("SKU-001", "Laptop", 1, "Device"));
        product.UpdateDetails(new ProductDetails("SKU-001", "Laptop", 1, null));
        Assert.Null(product.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDetails_WithInvalidSku_ShouldPreserveOriginalData(string? sku)
    {
        var product = CreateProduct();
        Assert.ThrowsAny<ArgumentException>(() =>
            product.UpdateDetails(new ProductDetails(sku!, "Monitor", 2, "Display")));
        AssertOriginalData(product);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDetails_WithInvalidName_ShouldPreserveOriginalData(string? name)
    {
        var product = CreateProduct();
        Assert.ThrowsAny<ArgumentException>(() =>
            product.UpdateDetails(new ProductDetails("SKU-002", name!, 2, "Display")));
        AssertOriginalData(product);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateDetails_WithInvalidCategoryId_ShouldPreserveOriginalData(int categoryId)
    {
        var product = CreateProduct();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            product.UpdateDetails(new ProductDetails("SKU-002", "Monitor", categoryId, "Display")));
        AssertOriginalData(product);
    }

    [Fact]
    public void UpdateDetails_WithInvalidDescription_ShouldPreserveOriginalData()
    {
        var product = CreateProduct();
        var description = new string('A', 501);
        Assert.Throws<ArgumentException>(() =>
            product.UpdateDetails(new ProductDetails("SKU-002", "Monitor", 2, description)));
        AssertOriginalData(product);
    }

    [Fact]
    public void UpdateDetails_WhenProductIsInactive_ShouldThrowException()
    {
        var product = CreateProduct();
        product.Deactivate();
        var exception = Assert.Throws<DomainRuleException>(() =>
            product.UpdateDetails(new ProductDetails("SKU-002", "Monitor", 2, "Display")));
        Assert.Equal(DomainRuleCode.InactiveProduct, exception.Code);
        AssertOriginalData(product);
        Assert.False(product.IsActive);
    }

    [Fact]
    public void Deactivate_WhenProductHasNoStock_ShouldSetIsActiveToFalse()
    {
        var product = CreateProduct();
        product.Deactivate();
        Assert.False(product.IsActive);
        AssertOriginalData(product);
    }

    [Fact]
    public void Deactivate_WhenProductIsAlreadyInactive_ShouldRemainInactive()
    {
        var product = CreateProduct();
        product.Deactivate();
        product.Deactivate();
        Assert.False(product.IsActive);
    }

    [Fact]
    public void RegisterEntry_WithValidQuantity_ShouldIncreaseStock()
    {
        var product = CreateProduct();
        product.RegisterEntry(10);
        product.RegisterEntry(5);
        Assert.Equal(15, product.Stock);
    }

    [Fact]
    public void RegisterExit_WithSufficientStock_ShouldDecreaseStock()
    {
        var product = CreateProduct();
        product.RegisterEntry(10);
        product.RegisterExit(4);
        Assert.Equal(6, product.Stock);
    }

    [Fact]
    public void RegisterExit_WithExactStock_ShouldLeaveZeroStock()
    {
        var product = CreateProduct();
        product.RegisterEntry(10);
        Assert.Equal(10, product.Stock);
        product.RegisterExit(10);
        Assert.Equal(0, product.Stock);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void RegisterEntry_WithInvalidQuantity_ShouldThrowException(int quantity)
    {
        var product = CreateProduct();
        product.RegisterEntry(10);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            product.RegisterEntry(quantity));
        Assert.Equal(10, product.Stock);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void RegisterExit_WithInvalidQuantity_ShouldThrowException(int quantity)
    {
        var product = CreateProduct();
        product.RegisterEntry(10);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            product.RegisterExit(quantity));
        Assert.Equal(10, product.Stock);
    }

    [Fact]
    public void RegisterExit_WithInsufficientStock_ShouldPreserveStock()
    {
        var product = CreateProduct();
        product.RegisterEntry(5);
        var exception = Assert.Throws<DomainRuleException>(() =>
            product.RegisterExit(6));
        Assert.Equal(DomainRuleCode.InsufficientStock, exception.Code);
        Assert.Equal(5, product.Stock);
    }

    [Fact]
    public void RegisterExit_WithoutStock_ShouldThrowException()
    {
        var product = CreateProduct();
        var exception = Assert.Throws<DomainRuleException>(() =>
            product.RegisterExit(1));
        Assert.Equal(DomainRuleCode.InsufficientStock, exception.Code);
        Assert.Equal(0, product.Stock);
    }

    [Fact]
    public void RegisterEntry_WhenStockWouldOverflow_ShouldPreserveStock()
    {
        var product = CreateProduct();
        product.RegisterEntry(int.MaxValue);
        Assert.Equal(int.MaxValue, product.Stock);
        var exception = Assert.Throws<DomainRuleException>(() =>
            product.RegisterEntry(1));
        Assert.Equal(DomainRuleCode.StockLimitExceeded, exception.Code);
        Assert.Equal(int.MaxValue, product.Stock);
    }

    [Fact]
    public void RegisterEntry_WhenProductIsInactive_ShouldThrowException()
    {
        var product = CreateProduct();
        product.Deactivate();
        var exception = Assert.Throws<DomainRuleException>(() =>
            product.RegisterEntry(10));
        Assert.Equal(DomainRuleCode.InactiveProduct, exception.Code);
        Assert.Equal(0, product.Stock);
    }

    [Fact]
    public void RegisterExit_WhenProductIsInactive_ShouldThrowException()
    {
        var product = CreateProduct();
        product.Deactivate();
        var exception = Assert.Throws<DomainRuleException>(() =>
            product.RegisterExit(1));
        Assert.Equal(DomainRuleCode.InactiveProduct, exception.Code);
        Assert.Equal(0, product.Stock);
    }

    [Fact]
    public void Deactivate_WhenProductHasStock_ShouldThrowException()
    {
        var product = CreateProduct();
        product.RegisterEntry(10);
        var exception = Assert.Throws<DomainRuleException>(() =>
            product.Deactivate());
        Assert.Equal(DomainRuleCode.ProductHasStock, exception.Code);
        Assert.True(product.IsActive);
        Assert.Equal(10, product.Stock);
    }

    [Fact]
    public void Deactivate_AfterRemovingAllStock_ShouldSetIsActiveToFalse()
    {
        var product = CreateProduct();
        product.RegisterEntry(10);
        Assert.Equal(10, product.Stock);
        product.RegisterExit(10);
        product.Deactivate();
        Assert.False(product.IsActive);
        Assert.Equal(0, product.Stock);
    }

    [Fact]
    public void UpdateDetails_WhenProductHasStock_ShouldPreserveStock()
    {
        var product = CreateProduct();
        product.RegisterEntry(10);
        product.UpdateDetails(new ProductDetails("SKU-002", "Monitor", 2, "Display"));
        Assert.Equal(10, product.Stock);
        Assert.Equal("SKU-002", product.SKU);
        Assert.Equal("Monitor", product.Name);
        Assert.Equal("Display", product.Description);
        Assert.Equal(2, product.CategoryId);
        Assert.True(product.IsActive);
    }

    [Fact]
    public void UpdateDetails_WithSkuExceedingMaximumLength_ShouldPreserveOriginalData()
    {
        var product = CreateProduct();
        var sku = new string('A', 51);
        Assert.Throws<ArgumentException>(() =>
            product.UpdateDetails(new ProductDetails(sku, "Monitor", 2, "Display")));
        AssertOriginalData(product);
    }

    [Fact]
    public void UpdateDetails_WithNameExceedingMaximumLength_ShouldPreserveOriginalData()
    {
        var product = CreateProduct();
        var name = new string('A', 151);
        Assert.Throws<ArgumentException>(() =>
            product.UpdateDetails(new ProductDetails("SKU-002", name, 2, "Display")));
        AssertOriginalData(product);
    }

    [Fact]
    public void Constructor_WithNullDetails_ShouldThrowException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new Product(null!));
        Assert.Equal("details", exception.ParamName);
    }

    [Fact]
    public void UpdateDetails_WithNullDetails_ShouldPreserveOriginalData()
    {
        var product = CreateProduct();
        var exception = Assert.Throws<ArgumentNullException>(() =>
            product.UpdateDetails(null!));
        Assert.Equal("details", exception.ParamName);
        AssertOriginalData(product);
        Assert.True(product.IsActive);
    }

    [Theory]
    [InlineData(47, true)]
    [InlineData(0, true)]
    [InlineData(0, false)]
    public void Restore_WithPersistedData_ShouldPreserveState(
    int stock,
    bool isActive)
    {
        var details = new ProductDetails(
            "SKU-001", "Laptop", 1, "Device");
        var product = Product.Restore(25, details, stock, isActive);
        Assert.Equal(25, product.Id);
        Assert.Equal("SKU-001", product.SKU);
        Assert.Equal("Laptop", product.Name);
        Assert.Equal(1, product.CategoryId);
        Assert.Equal("Device", product.Description);
        Assert.Equal(stock, product.Stock);
        Assert.Equal(isActive, product.IsActive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Restore_WithInvalidId_ShouldThrowException(int id)
    {
        var details = new ProductDetails("SKU-001", "Laptop", 1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Product.Restore(id, details, 0, true));
    }

    [Fact]
    public void Restore_WithNegativeStock_ShouldThrowException()
    {
        var details = new ProductDetails("SKU-001", "Laptop", 1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Product.Restore(25, details, -1, true));
    }

    [Fact]
    public void Restore_WithInactiveProductAndStock_ShouldThrowException()
    {
        var details = new ProductDetails("SKU-001", "Laptop", 1);
        Assert.Throws<ArgumentException>(() =>
            Product.Restore(25, details, 10, false));
    }

    private static Product CreateProduct()
    {
        return new Product(new ProductDetails("SKU-001", "Laptop", 1, "Device"));
    }

    private static void AssertOriginalData(Product product)
    {
        Assert.Equal("SKU-001", product.SKU);
        Assert.Equal("Laptop", product.Name);
        Assert.Equal("Device", product.Description);
        Assert.Equal(1, product.CategoryId);
        Assert.Equal(0, product.Stock);
        Assert.Equal(0, product.Id);
    }
}
