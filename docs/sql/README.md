# The vault's SQL

The schema of the vault is no longer a script to run by hand: `Stellaegis.Secrets.PostgreSQL` embeds it, as
`Sql/PostgreSql/0001_initial.sql`, and declares it to Primitives as `SecretsDatabase.Value`. It is applied by the
installation of the application, `DatabaseLifecycle.InstallAsync` of `Stellaegis.Primitives.Data`, under a role
that owns the schema, and verified by the application at every startup under its own role. The README of the
repository, § *The PostgreSQL vault*, says how.

`reset-vault.sql` deletes every secret, for development only.
