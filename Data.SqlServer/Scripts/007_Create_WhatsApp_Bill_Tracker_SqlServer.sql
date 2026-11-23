IF OBJECT_ID(N'dbo.WhatsApp_Bill_Tracker', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WhatsApp_Bill_Tracker (
        WhatsAppBillTrackerId BIGINT IDENTITY(1,1) CONSTRAINT PK_WhatsApp_Bill_Tracker PRIMARY KEY,
        Wamid NVARCHAR(255) NOT NULL,
        InvoiceNo NVARCHAR(100) NOT NULL,
        MobileNumber NVARCHAR(50) NOT NULL CONSTRAINT DF_WABT_Mobile DEFAULT(''),
        SentAt DATETIME2 NOT NULL CONSTRAINT DF_WABT_SentAt DEFAULT(SYSDATETIME()),
        Rating NVARCHAR(50) NULL,
        Reply NVARCHAR(500) NULL,
        UpdatedAt DATETIME2 NULL
    );

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WhatsApp_Bill_Tracker_Wamid' AND object_id = OBJECT_ID('dbo.WhatsApp_Bill_Tracker'))
        CREATE INDEX IX_WhatsApp_Bill_Tracker_Wamid ON dbo.WhatsApp_Bill_Tracker(Wamid);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WhatsApp_Bill_Tracker_InvoiceNo' AND object_id = OBJECT_ID('dbo.WhatsApp_Bill_Tracker'))
        CREATE INDEX IX_WhatsApp_Bill_Tracker_InvoiceNo ON dbo.WhatsApp_Bill_Tracker(InvoiceNo);
END
