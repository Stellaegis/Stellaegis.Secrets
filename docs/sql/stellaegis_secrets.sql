-- ============================================================================
-- Stellaegis Secrets — Database schema (vault), for DatabaseSecretsProvider
-- ============================================================================
-- Schema:   vault, in whichever database the provider's connection string names.
-- All constraints explicitly named.
--
-- Prerequisite: pgcrypto extension (symmetric encryption via pgp_sym_encrypt).
-- create extension needs a database administrator: run this script under such an account, or have
-- the extension created beforehand. The application itself connects under an ordinary role.
-- The passphrases come from DatabaseSecretsOptions (Stellaegis:Secrets:Keys:{1|2} by default).
-- Never store a passphrase in the database or in source code.
-- ============================================================================

create schema if not exists vault;

create extension if not exists pgcrypto;

-- ============================================================================
-- Secrets
-- ============================================================================
-- Encrypted storage of application secrets (connection strings, API keys,
-- OIDC secrets, etc.) as JSON encrypted via pgp_sym_encrypt.
--
-- Key (path): the text form of a SecretPath, {space}.{module}.{name}... joined by '.'
--             (e.g. acme.billing.smtp.password). The names after the module belong to it.
-- Value:      the encrypted secret, any text — often a JSON document.
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

comment on table  vault.secrets             is 'Secrets encrypted via pgp_sym_encrypt (pgcrypto). One secret = one path.';
comment on column vault.secrets.path        is 'Text form of a SecretPath: segments joined by a dot (e.g. acme.billing.smtp.password).';
comment on column vault.secrets.secret_json is 'The encrypted secret. Never decrypt it in a view or a log.';
comment on column vault.secrets.key_slot    is 'Active encryption slot (1 or 2, rotation modulo 2). Tells which passphrase to use for decryption.';

-- Index on the path prefix, for the purge of a compartment: every path under its prefix.
create index ix_vault_secrets_path_prefix
  on vault.secrets (path text_pattern_ops);
