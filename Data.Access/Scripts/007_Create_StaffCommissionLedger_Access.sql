-- ==========================================================
-- Migration: Create StaffCcommisionLedger Table
-- Engine: Microsoft Access (Jet / ACE OLEDB)
-- Description: Creates StaffCcommisionLedger table and indexes
-- ==========================================================

CREATE TABLE StaffCcommisionLedger
(
    AutoNo AUTOINCREMENT CONSTRAINT PK_StaffCcommisionLedger PRIMARY KEY,
    Nos INTEGER,
    Vno TEXT(50),
    Vdate DATETIME,
    StaffCode TEXT(50),
    StaffName TEXT(100),
    NetCommission CURRENCY,
    PaidAmount CURRENCY,
    BalanceAmount CURRENCY,
    Remarks LONGTEXT,
    EntryUser TEXT(50),
    EntryDate DATETIME,
    [Status] TEXT(50)
);

CREATE INDEX IX_StaffCcommisionLedger_Vno ON StaffCcommisionLedger (Vno);
CREATE INDEX IX_StaffCcommisionLedger_StaffCode ON StaffCcommisionLedger (StaffCode);
CREATE INDEX IX_StaffCcommisionLedger_Vdate ON StaffCcommisionLedger (Vdate);
