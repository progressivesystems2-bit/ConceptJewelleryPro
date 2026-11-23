-- ==========================================================
-- Migration: Create Settings Table
-- Engine: Microsoft Access (Jet / ACE OLEDB)
-- Description: Creates key-value Settings table
-- ==========================================================

CREATE TABLE Settings
(
    SettingsName TEXT(100) NOT NULL CONSTRAINT PK_Settings PRIMARY KEY,
    SettingsValue LONGTEXT
);

CREATE INDEX IX_Settings_SettingsName ON Settings (SettingsName);
