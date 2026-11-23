-- ==========================================================
-- Migration: Create RateStored Table
-- Engine: Microsoft SQL Server
-- Description: Creates rate log history table for Purity Rate Updates
-- ==========================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RateStored' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.RateStored
    (
        AutoNo INT IDENTITY(1,1) NOT NULL,
        Purity NVARCHAR(50) NOT NULL,
        Rate DECIMAL(18, 2) NOT NULL CONSTRAINT DF_RateStored_Rate DEFAULT (0),
        [Date] DATETIME NOT NULL CONSTRAINT DF_RateStored_Date DEFAULT (GETDATE()),
        [Time] DATETIME NOT NULL CONSTRAINT DF_RateStored_Time DEFAULT (GETDATE()),
        CONSTRAINT PK_RateStored PRIMARY KEY CLUSTERED (AutoNo)
    );

    CREATE NONCLUSTERED INDEX IX_RateStored_Purity ON dbo.RateStored(Purity);
    CREATE NONCLUSTERED INDEX IX_RateStored_Date ON dbo.RateStored([Date] DESC);
END
GO
