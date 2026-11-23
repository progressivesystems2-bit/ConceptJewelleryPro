-- ====================================================================
-- Concept Jewellery Pro — Product & Feature Module Licensing Migration
-- Run once in your Supabase Project -> SQL Editor
-- ====================================================================

ALTER TABLE public.app_licenses
    ADD COLUMN IF NOT EXISTS product_name TEXT NOT NULL DEFAULT 'GarmentsERP';

ALTER TABLE public.app_licenses
    ADD COLUMN IF NOT EXISTS enabled_modules TEXT;

CREATE INDEX IF NOT EXISTS idx_app_licenses_product_name
    ON public.app_licenses (product_name);
