SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories
    (
        Id INT IDENTITY(1,1) NOT NULL,
        Name NVARCHAR(150) COLLATE Latin1_General_100_CI_AS_SC NOT NULL,
        Description NVARCHAR(500) NULL,
        IsActive BIT NOT NULL
            CONSTRAINT DF_Categories_IsActive DEFAULT (1),

        CONSTRAINT PK_Categories PRIMARY KEY (Id),
        CONSTRAINT UQ_Categories_Name UNIQUE (Name),
        CONSTRAINT CK_Categories_Name_NotBlank CHECK
        (
            LEN(TRIM(NCHAR(9) + NCHAR(10) + NCHAR(11) +
                NCHAR(12) + NCHAR(13) + N' ' FROM Name)) > 0
        )
    );
END;

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        Id INT IDENTITY(1,1) NOT NULL,
        CategoryId INT NOT NULL,
        SKU NVARCHAR(50) COLLATE Latin1_General_100_CI_AS_SC NOT NULL,
        Name NVARCHAR(150) NOT NULL,
        Description NVARCHAR(500) NULL,
        Stock INT NOT NULL
            CONSTRAINT DF_Products_Stock DEFAULT (0),
        IsActive BIT NOT NULL
            CONSTRAINT DF_Products_IsActive DEFAULT (1),

        CONSTRAINT PK_Products PRIMARY KEY (Id),
        CONSTRAINT UQ_Products_SKU UNIQUE (SKU),

        CONSTRAINT FK_Products_Categories
            FOREIGN KEY (CategoryId)
            REFERENCES dbo.Categories (Id)
            ON DELETE NO ACTION,

        CONSTRAINT CK_Products_SKU_NotBlank CHECK
        (
            LEN(TRIM(NCHAR(9) + NCHAR(10) + NCHAR(11) +
                NCHAR(12) + NCHAR(13) + N' ' FROM SKU)) > 0
        ),
        CONSTRAINT CK_Products_Name_NotBlank CHECK
        (
            LEN(TRIM(NCHAR(9) + NCHAR(10) + NCHAR(11) +
                NCHAR(12) + NCHAR(13) + N' ' FROM Name)) > 0
        ),
        CONSTRAINT CK_Products_Stock_NonNegative
            CHECK (Stock >= 0),
        CONSTRAINT CK_Products_Inactive_Stock
            CHECK (IsActive = 1 OR Stock = 0)
    );

    CREATE INDEX IX_Products_CategoryId_IsActive
        ON dbo.Products (CategoryId, IsActive);
END;

IF OBJECT_ID(N'dbo.InventoryMovements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryMovements
    (
        Id INT IDENTITY(1,1) NOT NULL,
        ProductId INT NOT NULL,
        Type TINYINT NOT NULL,
        Quantity INT NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL
            CONSTRAINT DF_InventoryMovements_CreatedAt
                DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_InventoryMovements PRIMARY KEY (Id),

        CONSTRAINT FK_InventoryMovements_Products
            FOREIGN KEY (ProductId)
            REFERENCES dbo.Products (Id)
            ON DELETE NO ACTION,

        CONSTRAINT CK_InventoryMovements_Type
            CHECK (Type IN (1, 2)),
        CONSTRAINT CK_InventoryMovements_Quantity
            CHECK (Quantity > 0)
    );

    CREATE INDEX IX_InventoryMovements_ProductId_CreatedAt
        ON dbo.InventoryMovements (ProductId, CreatedAt);
END;

COMMIT TRANSACTION;

PRINT 'InventoryManagement schema is ready.';