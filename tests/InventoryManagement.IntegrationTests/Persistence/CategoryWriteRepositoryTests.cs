using Dapper;
using InventoryManagement.Application.Exceptions;
using InventoryManagement.Domain.Entities;
using InventoryManagement.Infrastructure.Persistence.Repositories.Write;
using InventoryManagement.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.IntegrationTests.Persistence;

[Collection("SqlServer integration")]
public class CategoryWriteRepositoryTests
    : IClassFixture<ReadRepositoryFixture>
{
    private readonly ReadRepositoryFixture _fixture;
    public CategoryWriteRepositoryTests(ReadRepositoryFixture fixture)
    {
        _fixture = fixture;
    }
    [Fact]
    public async Task CreateAsync_WithValidCategory_ShouldPersistCategory()
    {
        var repository = CreateRepository();
        var name = CreateUniqueName();
        var category = new Category($"  {name}  ", "  Description  ");
        var id = await repository.CreateAsync(
            category, CancellationToken.None);
        var persisted = await ReadCategoryAsync(id);
        Assert.True(id > 0);
        Assert.Equal(id, persisted.Id);
        Assert.Equal(name, persisted.Name);
        Assert.Equal("Description", persisted.Description);
        Assert.True(persisted.IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateAsync_WithDuplicateName_ShouldThrowConflict(
        bool isActive)
    {
        var repository = CreateRepository();
        var name = CreateUniqueName();
        await InsertCategoryAsync(name, isActive);
        var category = new Category(name.ToUpperInvariant());
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            repository.CreateAsync(category, CancellationToken.None));
        Assert.Equal(
            PersistenceRuleCode.DuplicateCategoryName, exception.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateAsync_WithInvalidEntityState_ShouldThrowException(
        bool isPersisted)
    {
        var repository = CreateRepository();
        var name = CreateUniqueName();
        var category = isPersisted
            ? Category.Restore(25, name, null, true)
            : new Category(name);
        if (!isPersisted)
        {
            category.Deactivate();
        }
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.CreateAsync(category, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldPersistChanges()
    {
        var repository = CreateRepository();
        var id = await InsertCategoryAsync(CreateUniqueName());
        var name = CreateUniqueName();
        await repository.UpdateAsync(
            id, $"  {name}  ", "  Updated description  ",
            CancellationToken.None);
        var persisted = await ReadCategoryAsync(id);
        Assert.Equal(id, persisted.Id);
        Assert.Equal(name, persisted.Name);
        Assert.Equal("Updated description", persisted.Description);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateName_ShouldPreserveOriginalData()
    {
        var repository = CreateRepository();
        var originalName = CreateUniqueName();
        var duplicateName = CreateUniqueName();
        var id = await InsertCategoryAsync(originalName);
        await InsertCategoryAsync(duplicateName);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            repository.UpdateAsync(id, duplicateName.ToUpperInvariant(),
                "New description", CancellationToken.None));
        Assert.Equal(
            PersistenceRuleCode.DuplicateCategoryName, exception.Code);
        var persisted = await ReadCategoryAsync(id);
        Assert.Equal(originalName, persisted.Name);
        Assert.Null(persisted.Description);
        Assert.True(persisted.IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateAsync_WithUnavailableCategory_ShouldThrowNotFound(
        bool exists)
    {
        var repository = CreateRepository();
        var id = exists
            ? await InsertCategoryAsync(CreateUniqueName(), false)
            : int.MaxValue;
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            repository.UpdateAsync(id, CreateUniqueName(), null,
                CancellationToken.None));
        Assert.Equal(PersistenceRuleCode.CategoryNotFound, exception.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeactivateAsync_WithoutActiveProducts_ShouldDeactivate(
        bool hasInactiveProduct)
    {
        var repository = CreateRepository();
        var id = await InsertCategoryAsync(CreateUniqueName());
        if (hasInactiveProduct)
        {
            await InsertProductAsync(id, false);
        }
        await repository.DeactivateAsync(id, CancellationToken.None);
        var persisted = await ReadCategoryAsync(id);
        Assert.False(persisted.IsActive);
        Assert.Equal(id, persisted.Id);
    }

    [Fact]
    public async Task DeactivateAsync_WithActiveProducts_ShouldPreserveCategory()
    {
        var repository = CreateRepository();
        var id = await InsertCategoryAsync(CreateUniqueName());
        await InsertProductAsync(id, true);
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            repository.DeactivateAsync(id, CancellationToken.None));
        Assert.Equal(
            PersistenceRuleCode.CategoryHasActiveProducts, exception.Code);
        var persisted = await ReadCategoryAsync(id);
        Assert.True(persisted.IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeactivateAsync_WithUnavailableCategory_ShouldThrowNotFound(
        bool exists)
    {
        var repository = CreateRepository();
        var id = exists
            ? await InsertCategoryAsync(CreateUniqueName(), false)
            : int.MaxValue;
        var exception = await Assert.ThrowsAsync<PersistenceRuleException>(() =>
            repository.DeactivateAsync(id, CancellationToken.None));
        Assert.Equal(PersistenceRuleCode.CategoryNotFound, exception.Code);
    }

    private CategoryWriteRepository CreateRepository()
    {
        return new CategoryWriteRepository(
            _fixture.CreateConnectionFactory());
    }

    private async Task<Category> ReadCategoryAsync(int id)
    {
        await using var context = _fixture.CreateContext();

        return await context.Categories
            .AsNoTracking()
            .SingleAsync(category => category.Id == id);
    }

    private async Task<int> InsertCategoryAsync(
        string name,
        bool isActive = true)
    {
        const string sql = """
            INSERT INTO dbo.Categories (Name, IsActive)
            OUTPUT INSERTED.Id
            VALUES (@Name, @IsActive);
            """;
        await using var connection = await _fixture
            .CreateConnectionFactory().OpenAsync(CancellationToken.None);

        return await connection.QuerySingleAsync<int>(

            sql, new { Name = name, IsActive = isActive });
    }

    private async Task InsertProductAsync(int categoryId, bool isActive)
    {
        const string sql = """
            INSERT INTO dbo.Products
                (CategoryId, SKU, Name, Stock, IsActive)
            VALUES
                (@CategoryId, @SKU, @Name, 0, @IsActive);
            """;
        await using var connection = await _fixture
            .CreateConnectionFactory().OpenAsync(CancellationToken.None);
        await connection.ExecuteAsync(sql, new
        {
            CategoryId = categoryId,
            SKU = $"TEST-{Guid.NewGuid():N}",
            Name = "Integration test product",
            IsActive = isActive
        });
    }

    private static string CreateUniqueName()
    {
        return $"Category-{Guid.NewGuid():N}";
    }
}
