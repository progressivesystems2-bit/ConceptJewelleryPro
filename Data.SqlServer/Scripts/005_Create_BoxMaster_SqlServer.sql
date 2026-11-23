-- ==========================================================
-- Migration: Create BoxMaster Table
-- Engine: Microsoft SQL Server
-- Description: Stores physical jewelry box classifications,
--              rack storage locations, and sequencing serials.
-- ==========================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BoxMaster]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[BoxMaster]
    (
        [BoxNo] NVARCHAR(50) NOT NULL CONSTRAINT PK_BoxMaster PRIMARY KEY,
        [BoxName] NVARCHAR(100) NULL,
        [Rack] NVARCHAR(50) NULL,
        [NumberOfBoxes] INT NULL DEFAULT 1,
        [BoxWt] DECIMAL(18, 3) NULL DEFAULT 0,
        [Serial] INT NULL,
        [Status] NVARCHAR(50) NULL DEFAULT 'Active',
        [Remarks] NVARCHAR(MAX) NULL,
        [CreatedDate] DATETIME2 NULL DEFAULT SYSUTCDATETIME(),
        [ModifiedDate] DATETIME2 NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE NONCLUSTERED INDEX IX_BoxMaster_Rack ON [dbo].[BoxMaster] ([Rack]);
    CREATE NONCLUSTERED INDEX IX_BoxMaster_Serial ON [dbo].[BoxMaster] ([Serial]);
END
