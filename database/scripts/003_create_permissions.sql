SET NOCOUNT ON;

USE master;
GO

IF SUSER_ID(N'inventory_app') IS NULL
BEGIN
    DECLARE @password NVARCHAR(128) =
        CONVERT(NVARCHAR(128),
            CONVERT(VARCHAR(128), 0x$(APP_PASSWORD_HEX)));

    DECLARE @statement NVARCHAR(MAX) =
        N'CREATE LOGIN [inventory_app] WITH PASSWORD = ' +
        QUOTENAME(@password, '''') +
        N', CHECK_POLICY = ON,
            CHECK_EXPIRATION = OFF,
            DEFAULT_DATABASE = [InventoryManagement];';

    EXEC sys.sp_executesql @statement;
END;
GO

USE InventoryManagement;
GO

IF USER_ID(N'inventory_app') IS NULL
BEGIN
    CREATE USER [inventory_app]
        FOR LOGIN [inventory_app]
        WITH DEFAULT_SCHEMA = [dbo];
END;

GRANT SELECT, INSERT
    ON OBJECT::dbo.Categories TO [inventory_app];

GRANT UPDATE
    ON OBJECT::dbo.Categories (Name, Description, IsActive)
    TO [inventory_app];

GRANT SELECT, INSERT
    ON OBJECT::dbo.Products TO [inventory_app];

GRANT UPDATE
    ON OBJECT::dbo.Products
        (CategoryId, SKU, Name, Description, Stock, IsActive)
    TO [inventory_app];

GRANT SELECT, INSERT
    ON OBJECT::dbo.InventoryMovements TO [inventory_app];

PRINT 'Inventory application permissions are ready.';