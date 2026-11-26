using InventoryManagement.Application.Models;

namespace InventoryManagement.Application.Products.Queries.GetProducts;

public sealed record GetProductsQuery(PageRequest Pagination);
