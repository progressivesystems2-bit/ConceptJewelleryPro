-- ==========================================================
-- Migration: Create PurchaseExpenses (PurchaseE & PurchaseESubform)
-- Engine: Microsoft SQL Server
-- Description: Creates header and line tables for Purchase Expenses
-- ==========================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchaseE' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.PurchaseE
    (
        Nos INT NOT NULL,
        VouType NVARCHAR(50) NOT NULL CONSTRAINT DF_PurchaseE_VouType DEFAULT ('PurchaseE'),
        VouNo NVARCHAR(50) NOT NULL,
        BillDate DATETIME NOT NULL CONSTRAINT DF_PurchaseE_BillDate DEFAULT (GETDATE()),
        AccountName NVARCHAR(150) NOT NULL,
        Head NVARCHAR(150) NULL,
        PartyName NVARCHAR(150) NULL,
        GSTIN NVARCHAR(50) NULL,
        SuppBillNo NVARCHAR(50) NULL,
        SuppDate DATETIME NULL,
        [Reverse] BIT NOT NULL CONSTRAINT DF_PurchaseE_Reverse DEFAULT (0),
        [Tax%] DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseE_TaxRate DEFAULT (0),
        GrossTotal DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseE_GrossTotal DEFAULT (0),
        TaxableAmount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseE_TaxableAmount DEFAULT (0),
        TaxAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseE_TaxAmt DEFAULT (0),
        AddLessNote NVARCHAR(150) NULL,
        Exclusive DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseE_Exclusive DEFAULT (0),
        ExclusiveAccount NVARCHAR(150) NULL,
        IGST DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseE_IGST DEFAULT (0),
        CGST DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseE_CGST DEFAULT (0),
        SGST DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseE_SGST DEFAULT (0),
        AddLess DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseE_AddLess DEFAULT (0),
        NetAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseE_NetAmt DEFAULT (0),
        Narration NVARCHAR(MAX) NULL,
        [Unique] NVARCHAR(50) NULL,
        [User] NVARCHAR(50) NULL,
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT DF_PurchaseE_Status DEFAULT ('Active'),
        EntryDate DATETIME NOT NULL CONSTRAINT DF_PurchaseE_EntryDate DEFAULT (GETDATE()),
        [Time] DATETIME NOT NULL CONSTRAINT DF_PurchaseE_Time DEFAULT (GETDATE()),
        Expense BIT NOT NULL CONSTRAINT DF_PurchaseE_Expense DEFAULT (1),
        Cform BIT NOT NULL CONSTRAINT DF_PurchaseE_Cform DEFAULT (0),
        Cleared BIT NOT NULL CONSTRAINT DF_PurchaseE_Cleared DEFAULT (0),
        [3b] BIT NOT NULL CONSTRAINT DF_PurchaseE_3b DEFAULT (0),
        InputTaken BIT NOT NULL CONSTRAINT DF_PurchaseE_InputTaken DEFAULT (0),
        CONSTRAINT PK_PurchaseE PRIMARY KEY CLUSTERED (VouNo)
    );

    CREATE NONCLUSTERED INDEX IX_PurchaseE_Nos ON dbo.PurchaseE(Nos);
    CREATE NONCLUSTERED INDEX IX_PurchaseE_AccountName ON dbo.PurchaseE(AccountName);
    CREATE NONCLUSTERED INDEX IX_PurchaseE_BillDate ON dbo.PurchaseE(BillDate DESC);
    CREATE NONCLUSTERED INDEX IX_PurchaseE_Unique ON dbo.PurchaseE([Unique]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchaseESubform' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.PurchaseESubform
    (
        AutoNo INT IDENTITY(1,1) NOT NULL,
        Vouno NVARCHAR(50) NOT NULL,
        SlNo NVARCHAR(50) NULL,
        ItemName NVARCHAR(150) NOT NULL,
        HSN NVARCHAR(50) NULL,
        Qty DECIMAL(18, 3) NOT NULL CONSTRAINT DF_PurchaseESubform_Qty DEFAULT (0),
        Rate DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseESubform_Rate DEFAULT (0),
        RateAmount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseESubform_RateAmount DEFAULT (0),
        DPer DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseESubform_DPer DEFAULT (0),
        DAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseESubform_DAmt DEFAULT (0),
        GrossAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseESubform_GrossAmt DEFAULT (0),
        [Count] INT NOT NULL CONSTRAINT DF_PurchaseESubform_Count DEFAULT (1),
        TrackIdNo NVARCHAR(50) NULL,
        TaxRate DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseESubform_TaxRate DEFAULT (0),
        TaxAmount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseESubform_TaxAmount DEFAULT (0),
        CONSTRAINT PK_PurchaseESubform PRIMARY KEY CLUSTERED (AutoNo)
    );

    CREATE NONCLUSTERED INDEX IX_PurchaseESubform_Vouno ON dbo.PurchaseESubform(Vouno);
END
GO
