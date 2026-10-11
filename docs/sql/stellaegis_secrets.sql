-- ============================================================================
-- Stellaegis Secrets — Database schema (vault)
-- ============================================================================
-- Database: stellaegis_secrets
-- Schema:   vault
-- No cross-database joins.
-- All constraints explicitly named.
--
-- Prerequisite: pgcrypto extension (symmetric encryption via pgp_sym_encrypt).
-- The passphrase is supplied by the STELLAEGIS_VAULT_PASSPHRASE environment variable.
-- Never store the passphrase in the database or in source code.
-- ============================================================================

create schema if not exists vault;

create extension if not exists pgcrypto;

-- ============================================================================
-- Secrets
-- ============================================================================
-- Encrypted storage of application secrets (connection strings, API keys,
-- OIDC secrets, etc.) as JSON encrypted via pgp_sym_encrypt.
--
-- Key (path): dotted hierarchical identifier (e.g. tenantDB.primary.postgresql.acme).
-- Value:      raw encrypted JSON — usually { "connectionString": "..." }.
--
-- Read:  SELECT pgp_sym_decrypt(secret_json, @passphrase)::text FROM vault.secrets WHERE path = @path
-- Write: INSERT ... ON CONFLICT (path) DO UPDATE SET secret_json = pgp_sym_encrypt(@value, @passphrase)
-- ============================================================================

create table vault.secrets (
  path        text        not null,
  secret_json bytea       not null,
  key_slot    smallint    not null default 1,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now(),

  constraint pk_vault_secrets          primary key (path),
  constraint ck_vault_secrets_key_slot check (key_slot in (1, 2))
);

comment on table  vault.secrets             is 'Secrets encrypted via pgp_sym_encrypt (pgcrypto). One secret = one dotted path.';
comment on column vault.secrets.path        is 'Hierarchical key: tier.name.provider.tenant_slug (e.g. tenantDB.primary.postgresql.acme).';
comment on column vault.secrets.secret_json is 'Encrypted JSON holding the raw secret. Never decrypt it in a view or a log.';
comment on column vault.secrets.key_slot    is 'Active encryption slot (1 or 2, rotation modulo 2). Tells which passphrase to use for decryption.';

-- Index on the path prefix for "all secrets of a tenant" style queries.
create index ix_vault_secrets_path_prefix
  on vault.secrets (path text_pattern_ops);

-- ============================================================================
-- Framework evolution tracking
-- ============================================================================

create table vault._schema_migrations (
  version      smallint     not null,
  script_name  text         not null,
  checksum     text         not null,
  applied_at   timestamptz  not null default now(),
  constraint pk_vault_schema_migrations primary key (version)
);

create table vault._seed_migrations (
  version      smallint     not null,
  description  text         not null,
  applied_at   timestamptz  not null default now(),
  constraint pk_vault_seed_migrations primary key (version)
);
