-- ==========================================================
-- Migration: Create BoxMaster Table
-- Engine: Microsoft Access (Jet / ACE OLEDB)
-- Description: Stores physical jewelry box classifications,
--              rack storage locations, and sequencing serials.
-- ==========================================================

CREATE TABLE BoxMaster
(
    BoxNo TEXT(50) NOT NULL CONSTRAINT PK_BoxMaster PRIMARY KEY,
    BoxName TEXT(100),
    Rack TEXT(50),
    NumberOfBoxes INTEGER,
    BoxWt DOUBLE,
    Serial INTEGER,
    Status TEXT(50),
    Remarks LONGTEXT,
    CreatedDate DATETIME,
    ModifiedDate DATETIME
);

CREATE INDEX IX_BoxMaster_Rack ON BoxMaster (Rack);
CREATE INDEX IX_BoxMaster_Serial ON BoxMaster (Serial);
