SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

INSERT INTO dbo.Categories (Name, Description, IsActive)
VALUES
    (N'Electronics', N'Electronic devices', 1),
    (N'Furniture', NULL, 1),
    (N'Accessories', NULL, 1),
    (N'Archived category', NULL, 0);

INSERT INTO dbo.Products
    (CategoryId, SKU, Name, Description, Stock, IsActive)
VALUES
    (1, N'SKU-001', N'Laptop', N'Portable computer', 7, 1),
    (1, N'SKU-002', N'Monitor', NULL, 0, 1),
    (2, N'SKU-003', N'Desk', NULL, 0, 1),
    (4, N'SKU-004', N'Archived product', NULL, 0, 0);

INSERT INTO dbo.InventoryMovements
    (ProductId, Type, Quantity, CreatedAt)
VALUES
    (1, 1, 10, '2026-01-01T12:00:00'),
    (1, 2, 3, '2026-01-01T13:00:00');

COMMIT TRANSACTION;
