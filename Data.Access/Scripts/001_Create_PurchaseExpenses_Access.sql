-- ==========================================================
-- Migration: Create PurchaseExpenses (PurchaseE & PurchaseESubform)
-- Engine: Microsoft Access (Jet / ACE OLEDB)
-- Description: Creates header and line tables for Purchase Expenses
-- ==========================================================

CREATE TABLE PurchaseE
(
    Nos INTEGER NOT NULL,
    VouType TEXT(50),
    VouNo TEXT(50) NOT NULL CONSTRAINT PK_PurchaseE PRIMARY KEY,
    BillDate DATETIME,
    AccountName TEXT(50),
    Head TEXT(50),
    PartyName TEXT(50),
    GSTIN TEXT(50),
    SuppBillNo TEXT(50),
    SuppDate DATETIME,
    [Reverse] YESNO,
    [Tax%] CURRENCY,
    GrossTotal CURRENCY,
    TaxableAmount CURRENCY,
    TaxAmt CURRENCY,
    AddLessNote TEXT(50),
    Exclusive CURRENCY,
    ExclusiveAccount TEXT(150),
    IGST CURRENCY,
    CGST CURRENCY,
    SGST CURRENCY,
    AddLess CURRENCY,
    NetAmt CURRENCY,
    Narration LONGTEXT,
    [Unique] TEXT(50),
    [User] TEXT(50),
    [Status] TEXT(50),
    EntryDate DATETIME,
    [Time] DATETIME,
    Expense YESNO,
    Cform YESNO,
    Cleared YESNO,
    [3b] YESNO,
    InputTaken YESNO
);

CREATE INDEX IX_PurchaseE_Nos ON PurchaseE (Nos);
CREATE INDEX IX_PurchaseE_AccountName ON PurchaseE (AccountName);
CREATE INDEX IX_PurchaseE_BillDate ON PurchaseE (BillDate);
CREATE INDEX IX_PurchaseE_Unique ON PurchaseE ([Unique]);

CREATE TABLE PurchaseESubform
(
    AutoNo AUTOINCREMENT CONSTRAINT PK_PurchaseESubform PRIMARY KEY,
    Vouno TEXT(50),
    SlNo TEXT(50),
    ItemName TEXT(150),
    HSN TEXT(50),
    Qty CURRENCY,
    Rate CURRENCY,
    RateAmount CURRENCY,
    DPer CURRENCY,
    DAmt CURRENCY,
    GrossAmt CURRENCY,
    [Count] INTEGER,
    TrackIdNo TEXT(50),
    TaxRate CURRENCY,
    TaxAmount CURRENCY
);

CREATE INDEX IX_PurchaseESubform_Vouno ON PurchaseESubform (Vouno);
