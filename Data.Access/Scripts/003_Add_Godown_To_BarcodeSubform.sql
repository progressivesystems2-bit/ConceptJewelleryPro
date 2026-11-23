-- ==========================================================
-- Migration: Add Godown column to BarcodeSubform
-- Engine: Microsoft Access (Jet / ACE OLEDB)
-- Description: Adds the Godown (warehouse/storage location) column
--              to the BarcodeSubform table. The column is optional
--              (nullable) and stores a short location code or name.
-- ==========================================================

ALTER TABLE BarcodeSubform ADD COLUMN Godown TEXT(100);
