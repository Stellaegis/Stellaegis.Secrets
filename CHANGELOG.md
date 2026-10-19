# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.0] — 2026-10-19

A secret path has a fixed shape, {space}.{module}.{name}…, so that every secret of a compartment lies
under one prefix, and the new `ISecretsPurge` removes them all. The package takes
`Stellaegis.Primitives` for its compartments, spaces, actors and modules. The five packages move
together to 4.0.0, skipping 2 and 3, to share the major version of the Stellaegis family.

### Changed

- **Breaking.** A `SecretPath` has a fixed shape, `{space}.{module}.{name}…`: a compartment, `_shared`
  or `_assembly`, then the name of an `IStellaegisModule`, then at least one name. Every secret of a
  compartment lies under one prefix, `TextPrefixOf(compartment)`. `SecretPath.From` is removed:
  build a path with `For<TModule>(space, names)` or `ForAssembly<TModule>(names)`. `Parse` refuses
  any other shape, and the advised naming grammar is gone.
- `Stellaegis.Secrets` depends on `Stellaegis.Primitives`, its core only, for `Space`, `Compartment`,
  `Actor` and `IStellaegisModule`.
- The options of the PostgreSQL and Scaleway providers are validated when the host starts, and a
  provider built by hand validates them in its constructor: both throw `OptionsValidationException`,
  naming the setting. The PostgreSQL provider used to throw `ArgumentException` or
  `InvalidOperationException` on its first resolution, and accepted an active slot other than 1 or 2.
- The HTTP endpoints carry no route name, so they can be mapped more than once in one host.

### Removed

- **Breaking.** `DatabaseSecretsOptions.Passphrase`, the single passphrase read as slot 1. Set
  `Keys:1` instead.
- The tables `vault._schema_migrations` and `vault._seed_migrations` leave the schema script: nothing
  writes them. The script notes that creating the pgcrypto extension needs a database administrator.

### Added

- `ISecretsPurge.PurgeCompartmentAsync(compartment, actor)` deletes every secret of a compartment,
  whatever module wrote it, and answers how many it deleted; replayed, it completes an interrupted
  purge. Implemented by the PostgreSQL provider, which registers it, and by the in-memory provider.
  It has no HTTP endpoint.

### Migrating from 1.x

- `SecretPath.From("tenantDB", "primary", "postgresql", "acme")` becomes, for instance,
  `SecretPath.For<DatabaseModule>(Compartment.Parse("acme"), "primary", "postgresql")`, where
  `DatabaseModule` is the `IStellaegisModule` of the code that writes and reads the secret.
- A secret that belongs to no compartment takes `SecretPath.ForAssembly<TModule>(names)`.
- Secrets stored under 1.x paths are not found under their new paths: rename them in the vault.
- A configuration that sets `Passphrase` sets `Keys:1` instead.

## [1.0.2] — 2026-10-14

### Changed

- Microsoft.Extensions dependencies move to 10.0.13, and Dapper to 2.1.89.
- The naming grammar advised in the README and in the documentation of `SecretPath` calls its
  last segment `{compartment}`, as the other Stellaegis packages do.

## [1.0.1] — 2026-10-12

### Fixed

- The HTTP example in the README and in the documentation of `MapSecretsEndpoints` registered the
  PostgreSQL provider with an `Action<DatabaseSecretsOptions>` overload that 1.0.0 does not have. It
  now reads `AddDatabaseSecretsProvider(builder.Configuration)`.

## [1.0.0] — 2026-10-11

First release as a standalone library.

### Added

- `ISecretsProvider` and `IMutableSecretsProvider`, which identify secrets by `SecretPath`: a
  sequence of segments made of `[A-Za-z0-9_-]`, with the unambiguous text form `a.b.c`.
- `ISecretNameFormat`, public on every provider, which translates a path into the vault's native name
  and back.
- Providers: in memory and from configuration (`Stellaegis.Secrets`), PostgreSQL with pgcrypto and
  two key slots (`Stellaegis.Secrets.PostgreSQL`), Scaleway Secret Manager
  (`Stellaegis.Secrets.Scaleway`).
- `MapSecretsEndpoints` and `MapSecretsEndpointsAnonymous`, which expose a provider over HTTP
  (`Stellaegis.Secrets.AspNetCore`), and the matching client (`Stellaegis.Secrets.Client`), whose
  authentication the caller attaches.

[4.0.0]: https://github.com/Stellaegis/Stellaegis.Secrets/releases/tag/v4.0.0
[1.0.2]: https://github.com/Stellaegis/Stellaegis.Secrets/releases/tag/v1.0.2
[1.0.1]: https://github.com/Stellaegis/Stellaegis.Secrets/releases/tag/v1.0.1
[1.0.0]: https://github.com/Stellaegis/Stellaegis.Secrets/releases/tag/v1.0.0
