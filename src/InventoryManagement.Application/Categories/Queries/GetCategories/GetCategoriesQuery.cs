using InventoryManagement.Application.Models;

namespace InventoryManagement.Application.Categories.Queries.GetCategories;

public sealed record GetCategoriesQuery(PageRequest Pagination);
