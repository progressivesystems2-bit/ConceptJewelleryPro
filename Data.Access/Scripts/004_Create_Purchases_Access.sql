-- ==========================================================
-- Migration: Create Purchases and PurchaseSubform
-- Engine: Microsoft Access (Jet / ACE OLEDB)
-- Description: Creates header and line tables for Purchase Module with multi-tax support
-- ==========================================================

CREATE TABLE Purchases
(
    VouNo TEXT(50) NOT NULL CONSTRAINT PK_Purchases PRIMARY KEY,
    VouType TEXT(50),
    BillDate DATETIME,
    DueDate DATETIME,
    SuppBillNo TEXT(50),
    SuppDate DATETIME,
    Phone TEXT(50),
    AccountName TEXT(150),
    CustomerName TEXT(150),
    CustomerCode TEXT(50),
    Address TEXT(255),
    Address2 TEXT(255),
    City TEXT(50),
    Pincode TEXT(20),
    Email TEXT(100),
    VatNo TEXT(50),
    VatType TEXT(50),
    [Reverse] YESNO,
    [Check] YESNO,
    BillCanceled YESNO,
    GrossAmt CURRENCY,
    DiscountAllowed CURRENCY,
    TaxableAmount CURRENCY,
    Tax CURRENCY,
    TaxAmt CURRENCY,
    CGSTAmt CURRENCY,
    SGSTAmt CURRENCY,
    IGSTAmt CURRENCY,
    [AddLess] CURRENCY,
    [AddLessNote] TEXT(100),
    [NetAmt] CURRENCY,
    [Remarks] MEMO,
    [Narration] MEMO,
    [Note] TEXT(255),
    EntryUser TEXT(50),
    EntryDate DATETIME,
    EntryTime DATETIME,
    ComputerName TEXT(50),
    [Status] TEXT(50),
    InOut TEXT(10),
    GrWt CURRENCY,
    NtWt CURRENCY,
    TotalQty INTEGER
);

CREATE INDEX IX_Purchases_BillDate ON Purchases (BillDate);
CREATE INDEX IX_Purchases_VouType ON Purchases (VouType);
CREATE INDEX IX_Purchases_AccountName ON Purchases (AccountName);
CREATE INDEX IX_Purchases_SuppBillNo ON Purchases (SuppBillNo);

CREATE TABLE PurchaseSubform
(
    AutoNo AUTOINCREMENT CONSTRAINT PK_PurchaseSubform PRIMARY KEY,
    VouNo TEXT(50),
    SlNo INTEGER,
    Barcode TEXT(50),
    OldBarcode TEXT(50),
    PcRate YESNO,
    ItemName TEXT(150),
    Purity TEXT(50),
    HSN TEXT(50),
    ENote TEXT(255),
    Qty INTEGER,
    GrWt CURRENCY,
    LessWt CURRENCY,
    NtWt CURRENCY,
    Rate CURRENCY,
    Amount CURRENCY,
    MetalValue CURRENCY,
    Making CURRENCY,
    MperGr CURRENCY,
    StoneValue CURRENCY,
    DisPer CURRENCY,
    DisAmt CURRENCY,
    GrossAmt CURRENCY,
    TaxableAmount CURRENCY,
    GstPer CURRENCY,
    GstAmt CURRENCY,
    TotalAmt CURRENCY,
    IsDiscount YESNO
);

CREATE INDEX IX_PurchaseSubform_VouNo ON PurchaseSubform (VouNo);
CREATE INDEX IX_PurchaseSubform_ItemName ON PurchaseSubform (ItemName);
CREATE INDEX IX_PurchaseSubform_Barcode ON PurchaseSubform (Barcode);
