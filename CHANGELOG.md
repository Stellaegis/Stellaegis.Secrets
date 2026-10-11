# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[1.0.0]: https://github.com/Stellaegis/Stellaegis.Secrets/releases/tag/v1.0.0
