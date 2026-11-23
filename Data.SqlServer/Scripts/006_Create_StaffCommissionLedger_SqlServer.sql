-- ==========================================================
-- Migration: Create StaffCcommisionLedger Table
-- Engine: Microsoft SQL Server
-- Description: Creates StaffCcommisionLedger table and indexes
-- ==========================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StaffCcommisionLedger' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.StaffCcommisionLedger
    (
        AutoNo INT IDENTITY(1,1) NOT NULL,
        Nos INT NULL,
        Vno NVARCHAR(50) NULL,
        Vdate DATETIME NULL,
        StaffCode NVARCHAR(50) NULL,
        StaffName NVARCHAR(100) NULL,
        NetCommission DECIMAL(18,2) NULL,
        PaidAmount DECIMAL(18,2) NULL,
        BalanceAmount DECIMAL(18,2) NULL,
        Remarks NVARCHAR(MAX) NULL,
        EntryUser NVARCHAR(50) NULL,
        EntryDate DATETIME NULL,
        [Status] NVARCHAR(50) NULL,
        CONSTRAINT PK_StaffCcommisionLedger PRIMARY KEY CLUSTERED (AutoNo)
    );

    CREATE NONCLUSTERED INDEX IX_StaffCcommisionLedger_Vno ON dbo.StaffCcommisionLedger(Vno);
    CREATE NONCLUSTERED INDEX IX_StaffCcommisionLedger_StaffCode ON dbo.StaffCcommisionLedger(StaffCode);
    CREATE NONCLUSTERED INDEX IX_StaffCcommisionLedger_Vdate ON dbo.StaffCcommisionLedger(Vdate);
END
GO
