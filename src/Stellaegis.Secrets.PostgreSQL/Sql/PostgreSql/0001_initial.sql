-- ============================================================================
-- Stellaegis.Secrets — the vault schema, script 0001
-- ============================================================================
-- Applied by the installation of the application (DatabaseLifecycle.InstallAsync of
-- Stellaegis.Primitives.Data), under the role that owns the schema; the schema and the pgcrypto
-- extension are created by the installation before this script. The application connects under
-- an ordinary role, granted its data rights by the installation.
--
-- The passphrases come from DatabaseSecretsOptions (Stellaegis:Secrets:Keys:{1|2} by default).
-- Never store a passphrase in the database or in source code.
--
-- No row-level security: a secret carries its space in its path, under one prefix per compartment,
-- and the vault is reached by the application's own code, never in a space of a request.
-- ============================================================================

-- Encrypted storage of application secrets (connection strings, API keys,
-- OIDC secrets, etc.) as text encrypted via pgp_sym_encrypt.
--
-- Key (path): the text form of a SecretPath, {space}.{module}.{name}... joined by '.'
--             (e.g. acme.billing.smtp.password). The names after the module belong to it.
-- Value:      the encrypted secret, any text — often a JSON document.

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
comment on column vault.secrets.key_slot    is 'Encryption slot (1 or 2, rotation modulo 2). Tells which passphrase decrypts the row.';

-- Index on the path prefix, for the purge of a compartment: every path under its prefix.
create index ix_vault_secrets_path_prefix
  on vault.secrets (path text_pattern_ops);
