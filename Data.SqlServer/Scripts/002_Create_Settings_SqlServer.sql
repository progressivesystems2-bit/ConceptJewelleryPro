-- ==========================================================
-- Migration: Create Settings Table
-- Engine: Microsoft SQL Server
-- Description: Creates key-value Settings table
-- ==========================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Settings' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.Settings
    (
        SettingsName NVARCHAR(100) NOT NULL,
        SettingsValue NVARCHAR(MAX) NULL,
        CONSTRAINT PK_Settings PRIMARY KEY CLUSTERED (SettingsName)
    );

    CREATE NONCLUSTERED INDEX IX_Settings_SettingsName ON dbo.Settings(SettingsName);
END
GO
