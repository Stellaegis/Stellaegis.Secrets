# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [5.0.0] — 2026-10-21

Secrets becomes a module of the Stellaegis family on Primitives 5.0.0: it declares itself, its operations and its
database; its routes carry an operation each and the purge reaches the wire; a missing secret is one error, with one
code, on both sides of it. The five packages move together to 5.0.0, the major version of the family.

### Migrating from 4.0.0

In the order a host follows it:

1. The packages reference `Stellaegis.Primitives` 5.0.0; `Stellaegis.Secrets` takes `Stellaegis.Primitives.Hosting`,
   `.PostgreSQL` takes `.Data`, `.AspNetCore` takes `Stellaegis.Primitives.AspNetCore`.
2. A host mapping the routes declares a frame with compartments and a request context:
   `AddStellaegisFrame(ApplicationFrame.ManyCompartments().WithSharedSpace())` and
   `AddStellaegisRequestContext(...)`. `MapSecretsEndpoints(authorizationPolicy)` becomes
   `MapSecretsEndpoints().RequireAuthorization(policy)`, and `MapSecretsEndpointsAnonymous()` becomes
   `MapSecretsEndpoints().AllowAnonymous()`: the startup check of Primitives refuses a route left without either.
3. A caller catching `KeyNotFoundException` catches `SecretNotFoundException`, whose `Error.Code` is
   `Secret.NotFound`; the client raises the same exception from the host's answer.
4. A PostgreSQL vault is installed by `DatabaseLifecycle.InstallAsync([SecretsDatabase.Value], frame, openAsAdmin,
   appRole)` rather than by running `docs/sql/stellaegis_secrets.sql`. A vault created by the 4.0.0 script keeps its
   table: the installation records the script as applied only when it runs it, so reinstall on an empty schema, or
   record the script by hand — a row of `vault._schema_migrations` with version `1`, `0001_initial.sql`, and the
   checksum `SqlScript.ChecksumOf(script.Sql)` gives: SHA-256 of the text, in lowercase hexadecimal. The application
   verifies the vault at startup with `AddStellaegisDatabaseVerification()`.
5. A client whose configuration is `Stellaegis:Secrets:Client` changes nothing: `BaseUrl` stays, `Timeout` is read
   from the same section, ten seconds by default.

### Changed

- **Breaking.** `MapSecretsEndpoints()` takes no policy and maps the routes of the module, `BelongsTo<SecretsModule>()`,
  with a request context, acting on spaces, with an `Operation` per route: `secrets.read` on `POST secrets/read` and
  `POST secrets/get`, `secrets.write` on `POST secrets/set`, `secrets.delete` on `POST secrets/delete`. The host
  declares the authorization on the returned group, or per operation, or opens the routes with `AllowAnonymous()`.
  `MapSecretsEndpointsAnonymous` is removed.
- **Breaking.** A secret that does not exist is a `SecretNotFoundException`, a `UserFacingException` with the code
  `Secret.NotFound`, from every provider and from the client, which reconstructs it from the host's problem
  details. `KeyNotFoundException` is no longer thrown. A request with no path, or a malformed one, answers `400`
  with the code `SecretPath.Invalid`; a `set` without a value, `Secret.ValueRequired`.
- **Breaking.** `Stellaegis.Secrets.PostgreSQL` declares the vault's database as `SecretsDatabase.Value`, a
  `ModuleDatabase` of Primitives with the script `0001_initial.sql` embedded and `pgcrypto` required, and
  `AddDatabaseSecretsProvider` registers it for the verification at startup. The script creates neither the schema
  nor the extension: the installation does. `docs/sql/stellaegis_secrets.sql` leaves the repository.
- `DatabaseSecretsOptions` holds its passphrases as the `KeySlots` of Primitives, and the options of the three
  adapters implement `IValidatableOptions`, registered by `AddStellaegisOptions`: the configuration names
  (`ConnectionString`, `ActiveKeySlot`, `Keys:1`, `Keys:2`) do not change; the validation message is prefixed with
  the name of the options. The validator classes are removed.
- `VaultClientOptions` is a `StellaegisClientOptions`, and `AddStellaegisHttpVaultClient` registers the client with
  `AddStellaegisHttpClient`: `Timeout` is configurable, ten seconds by default. The client reads one secret through
  `POST secrets/read` rather than through the batch route.
- The contracts of the HTTP API leave `Stellaegis.Secrets`: each end of the wire holds its own, internal.
- The PostgreSQL provider is one singleton, as the client's forwards are per resolution of the HTTP client factory.

### Added

- `SecretsModule`, the module `secrets`, declared `OptionallyCompartmented`, accepting the shared space, with its
  four operations and the `owner_isolation` capability as a compatibility. `AddStellaegisSecrets()` declares it once,
  and every registration of an adapter calls it.
- `MapSecretsAdminEndpoints()`: the purge of a compartment over HTTP, `DELETE secrets/compartments/{compartment}`,
  `secrets.purge`, which records the actor the host authenticated. The client implements `ISecretsPurge` against
  it.

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

[5.0.0]: https://github.com/Stellaegis/Stellaegis.Secrets/releases/tag/v5.0.0
[4.0.0]: https://github.com/Stellaegis/Stellaegis.Secrets/releases/tag/v4.0.0
[1.0.2]: https://github.com/Stellaegis/Stellaegis.Secrets/releases/tag/v1.0.2
[1.0.1]: https://github.com/Stellaegis/Stellaegis.Secrets/releases/tag/v1.0.1
[1.0.0]: https://github.com/Stellaegis/Stellaegis.Secrets/releases/tag/v1.0.0
