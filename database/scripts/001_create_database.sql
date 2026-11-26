SET NOCOUNT ON;

IF DB_ID(N'InventoryManagement') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [InventoryManagement]
        COLLATE Latin1_General_100_CI_AS_SC;');
END;

PRINT 'InventoryManagement database is ready.';