-- ============================================================================
-- Stellaegis Secrets — Reset script for schema: vault
-- ============================================================================
-- WARNING: Destroys ALL data in the vault schema. Irreversible.
-- Intended for development/pre-release reset only.
-- Usage: psql -d <database> -f reset-vault.sql
-- ============================================================================

BEGIN;

DELETE FROM vault.secrets;

COMMIT;
