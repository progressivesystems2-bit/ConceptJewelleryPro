-- ==========================================================
-- Migration: Create Purchases and PurchaseSubform
-- Engine: Microsoft SQL Server
-- Description: Creates header and line tables for Purchase Module with multi-tax support
-- ==========================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Purchases' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.Purchases
    (
        VouNo NVARCHAR(50) NOT NULL,
        VouType NVARCHAR(50) NOT NULL CONSTRAINT DF_Purchases_VouType DEFAULT ('Purchase'),
        BillDate DATETIME NOT NULL CONSTRAINT DF_Purchases_BillDate DEFAULT (GETDATE()),
        DueDate DATETIME NOT NULL CONSTRAINT DF_Purchases_DueDate DEFAULT (GETDATE()),
        SuppBillNo NVARCHAR(50) NULL,
        SuppDate DATETIME NULL,
        Phone NVARCHAR(50) NULL,
        AccountName NVARCHAR(150) NOT NULL,
        CustomerName NVARCHAR(150) NULL,
        CustomerCode NVARCHAR(50) NULL,
        Address NVARCHAR(255) NULL,
        Address2 NVARCHAR(255) NULL,
        City NVARCHAR(50) NULL CONSTRAINT DF_Purchases_City DEFAULT ('CUTTACK'),
        Pincode NVARCHAR(20) NULL,
        Email NVARCHAR(100) NULL,
        VatNo NVARCHAR(50) NULL,
        VatType NVARCHAR(50) NULL,
        [Reverse] BIT NOT NULL CONSTRAINT DF_Purchases_Reverse DEFAULT (0),
        [Check] BIT NOT NULL CONSTRAINT DF_Purchases_Check DEFAULT (0),
        BillCanceled BIT NOT NULL CONSTRAINT DF_Purchases_BillCanceled DEFAULT (0),
        GrossAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Purchases_GrossAmt DEFAULT (0),
        DiscountAllowed DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Purchases_DiscountAllowed DEFAULT (0),
        TaxableAmount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Purchases_TaxableAmount DEFAULT (0),
        Tax DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Purchases_Tax DEFAULT (0),
        TaxAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Purchases_TaxAmt DEFAULT (0),
        CGSTAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Purchases_CGSTAmt DEFAULT (0),
        SGSTAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Purchases_SGSTAmt DEFAULT (0),
        IGSTAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Purchases_IGSTAmt DEFAULT (0),
        AddLess DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Purchases_AddLess DEFAULT (0),
        AddLessNote NVARCHAR(100) NULL,
        NetAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_Purchases_NetAmt DEFAULT (0),
        Remarks NVARCHAR(MAX) NULL,
        Narration NVARCHAR(MAX) NULL,
        Note NVARCHAR(255) NULL,
        EntryUser NVARCHAR(50) NULL,
        EntryDate DATETIME NOT NULL CONSTRAINT DF_Purchases_EntryDate DEFAULT (GETDATE()),
        EntryTime DATETIME NOT NULL CONSTRAINT DF_Purchases_EntryTime DEFAULT (GETDATE()),
        ComputerName NVARCHAR(50) NULL,
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT DF_Purchases_Status DEFAULT ('Saved'),
        InOut NVARCHAR(10) NOT NULL CONSTRAINT DF_Purchases_InOut DEFAULT ('In'),
        GrWt DECIMAL(18, 3) NOT NULL CONSTRAINT DF_Purchases_GrWt DEFAULT (0),
        NtWt DECIMAL(18, 3) NOT NULL CONSTRAINT DF_Purchases_NtWt DEFAULT (0),
        TotalQty INT NOT NULL CONSTRAINT DF_Purchases_TotalQty DEFAULT (0),
        CONSTRAINT PK_Purchases PRIMARY KEY CLUSTERED (VouNo)
    );

    CREATE NONCLUSTERED INDEX IX_Purchases_BillDate ON dbo.Purchases(BillDate DESC);
    CREATE NONCLUSTERED INDEX IX_Purchases_VouType ON dbo.Purchases(VouType);
    CREATE NONCLUSTERED INDEX IX_Purchases_AccountName ON dbo.Purchases(AccountName);
    CREATE NONCLUSTERED INDEX IX_Purchases_SuppBillNo ON dbo.Purchases(SuppBillNo);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchaseSubform' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.PurchaseSubform
    (
        AutoNo INT IDENTITY(1,1) NOT NULL,
        VouNo NVARCHAR(50) NOT NULL,
        SlNo INT NOT NULL CONSTRAINT DF_PurchaseSubform_SlNo DEFAULT (1),
        Barcode NVARCHAR(50) NULL,
        OldBarcode NVARCHAR(50) NULL,
        PcRate BIT NOT NULL CONSTRAINT DF_PurchaseSubform_PcRate DEFAULT (0),
        ItemName NVARCHAR(150) NOT NULL,
        Purity NVARCHAR(50) NULL,
        HSN NVARCHAR(50) NULL,
        ENote NVARCHAR(255) NULL,
        Qty INT NOT NULL CONSTRAINT DF_PurchaseSubform_Qty DEFAULT (1),
        GrWt DECIMAL(18, 3) NOT NULL CONSTRAINT DF_PurchaseSubform_GrWt DEFAULT (0),
        LessWt DECIMAL(18, 3) NOT NULL CONSTRAINT DF_PurchaseSubform_LessWt DEFAULT (0),
        NtWt DECIMAL(18, 3) NOT NULL CONSTRAINT DF_PurchaseSubform_NtWt DEFAULT (0),
        Rate DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_Rate DEFAULT (0),
        Amount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_Amount DEFAULT (0),
        MetalValue DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_MetalValue DEFAULT (0),
        Making DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_Making DEFAULT (0),
        MperGr DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_MperGr DEFAULT (0),
        StoneValue DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_StoneValue DEFAULT (0),
        DisPer DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_DisPer DEFAULT (0),
        DisAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_DisAmt DEFAULT (0),
        GrossAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_GrossAmt DEFAULT (0),
        TaxableAmount DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_TaxableAmount DEFAULT (0),
        GstPer DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_GstPer DEFAULT (3.00),
        GstAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_GstAmt DEFAULT (0),
        TotalAmt DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PurchaseSubform_TotalAmt DEFAULT (0),
        IsDiscount BIT NOT NULL CONSTRAINT DF_PurchaseSubform_IsDiscount DEFAULT (0),
        CONSTRAINT PK_PurchaseSubform PRIMARY KEY CLUSTERED (AutoNo),
        CONSTRAINT FK_PurchaseSubform_Purchases FOREIGN KEY (VouNo) REFERENCES dbo.Purchases (VouNo) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_PurchaseSubform_VouNo ON dbo.PurchaseSubform(VouNo);
    CREATE NONCLUSTERED INDEX IX_PurchaseSubform_ItemName ON dbo.PurchaseSubform(ItemName);
    CREATE NONCLUSTERED INDEX IX_PurchaseSubform_Barcode ON dbo.PurchaseSubform(Barcode);
END
GO
