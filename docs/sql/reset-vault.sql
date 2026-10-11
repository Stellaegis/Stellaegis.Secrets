-- ============================================================================
-- Stellaegis — Reset script for schema: vault (Secrets)
-- ============================================================================
-- WARNING: Destroys ALL data in the vault schema. Irreversible.
-- Intended for development/pre-release reset only.
-- Usage: psql -U stellaegis -d stellaegis -f reset-vault.sql
-- ============================================================================

BEGIN;

DELETE FROM vault.secrets;

COMMIT;
