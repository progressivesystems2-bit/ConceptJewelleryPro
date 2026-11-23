-- ==========================================================
-- Migration 005: Create CustomerRequirement Table
-- Engine: Microsoft Access (Jet / ACE OLEDB)
-- Description: Stores customer jewellery requirement requests
--              logged via the mobile app.
-- Note: This table is also auto-created by
--       AccessCustomerRequirementRepository on first use.
-- ==========================================================

CREATE TABLE [CustomerRequirement] (
    [Id]             AUTOINCREMENT CONSTRAINT PK_CustomerRequirement PRIMARY KEY,
    [EntryDate]      DATETIME,
    [CustomerName]   TEXT(100),
    [CustomerMobile] TEXT(20),
    [ItemName]       TEXT(150),
    [Description]    LONGTEXT,
    [Size]           TEXT(50),
    [WeightRange]    TEXT(50),
    [Purity]         TEXT(50),
    [Remarks]        LONGTEXT,
    [EntryUser]      TEXT(50)
);

CREATE INDEX IX_CustomerReq_EntryDate     ON [CustomerRequirement] ([EntryDate]);
CREATE INDEX IX_CustomerReq_CustomerMobile ON [CustomerRequirement] ([CustomerMobile]);
