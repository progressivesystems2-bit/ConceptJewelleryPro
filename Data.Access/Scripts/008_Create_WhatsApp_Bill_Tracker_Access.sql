CREATE TABLE [WhatsApp_Bill_Tracker]
(
    [WhatsAppBillTrackerId] AUTOINCREMENT CONSTRAINT [PK_WhatsApp_Bill_Tracker] PRIMARY KEY,
    [Wamid] TEXT(255),
    [InvoiceNo] TEXT(100),
    [MobileNumber] TEXT(50),
    [SentAt] DATETIME,
    [Rating] TEXT(50),
    [Reply] LONGTEXT,
    [UpdatedAt] DATETIME
);
