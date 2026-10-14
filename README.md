# Stellaegis.Secrets

Stellaegis.Secrets is a small .NET library for reading and writing application secrets —
connection strings, API keys, client secrets — through one interface, whatever vault holds them.
Your code depends on `ISecretsProvider`. An adapter connects that interface to a concrete store:
your configuration, a PostgreSQL table encrypted with pgcrypto, Scaleway Secret Manager, or a remote
host reached over HTTP.

The library targets .NET 10 and is released under the MIT license.

## Packages

The library is split so that you only take the dependencies of the stores you use.

| Package | Contents | Depends on |
| --- | --- | --- |
| `Stellaegis.Secrets` | The interfaces, `SecretPath`, and two providers for development: in memory and from configuration | Microsoft.Extensions abstractions |
| `Stellaegis.Secrets.PostgreSQL` | A self-hosted vault: secrets encrypted by pgcrypto in a PostgreSQL table | Npgsql, Dapper |
| `Stellaegis.Secrets.Scaleway` | A read-only provider for Scaleway Secret Manager | `Microsoft.Extensions.Http` |
| `Stellaegis.Secrets.AspNetCore` | Endpoints that expose a provider over HTTP | ASP.NET Core |
| `Stellaegis.Secrets.Client` | A provider that calls those endpoints from another process | `Microsoft.Extensions.Http` |

## Identifying a secret: `SecretPath`

A secret is identified by a `SecretPath`, a sequence of one or more segments.

```csharp
var path = SecretPath.From("tenantDB", "primary", "postgresql", "acme");
var key  = SecretPath.From("smtp-password");
var same = SecretPath.Parse("tenantDB.primary.postgresql.acme");
```

Each segment is non-empty and uses only letters, digits, `_` and `-`. A segment never contains a
dot, so the text form `a.b.c` — produced by `ToString()` and read by `Parse` — cannot be ambiguous.
That text form is convenient in configuration files and logs.

Vaults name their secrets in different ways. HashiCorp Vault and Scaleway organise them in folders
separated by `/`. Azure Key Vault and Google Secret Manager use flat names with a restricted set of
characters. A path made of segments lets each adapter build the name its vault expects, so your code
never handles a separator.

### Native names: `ISecretNameFormat`

Each adapter translates a path into its vault's native name through an `ISecretNameFormat`. The
format is public, so that you can find a secret in the vault's console from its path, or move
secrets from one vault to another.

| Provider | Native name of `tenantDB.primary.postgresql.acme` |
| --- | --- |
| Configuration, PostgreSQL | `tenantDB.primary.postgresql.acme` |
| Scaleway | the secret `acme` in the folder `/tenantDB/primary/postgresql` |

`Join` turns a path into a native name, and `Split` turns a native name back into a path.
`Split(Join(path))` always returns the original path. A format that cannot represent a path refuses
it with an error, rather than give two different paths the same name.

To migrate between two vaults, read the native names from the first one, convert each of them with
the first format's `Split`, then write the secrets to the second vault, which applies its own format.

### A naming grammar, as advice

The library gives no meaning to segments. One grammar has proved useful, and you are free to use
another:

```text
{usage}.{name}.{provider}.{compartment}      e.g.  tenantDB.primary.postgresql.acme
```

It comes from asking the usual questions about a secret — what it is for, which instance, which
technology, and who it belongs to — so that none is forgotten. Its benefit is that paths written
this way map directly to the folders of a hierarchical vault.

Its drawback appears with flat vaults. Azure Key Vault, for instance, joins segments with `-`, so a
compartment whose name contains `-` cannot be represented there unambiguously.

## Choosing a provider

**In memory** (`InMemorySecretsProvider`). Secrets live in a dictionary that you fill with
`AddSecret`. It suits tests and local development. Nothing persists, and the provider logs a warning
at creation so that it is not used in production by mistake.

**Configuration** (`ConfigurationSecretsProvider`). Secrets are keys of one configuration section,
`Stellaegis:Secrets:Configuration` by default. Combined with .NET User Secrets or environment
variables, it keeps secrets out of source control without running a vault. The secrets are as safe
as the configuration source that holds them, and the provider is read-only.

```json
{
  "Stellaegis": {
    "Secrets": {
      "Configuration": {
        "tenantDB.primary.postgresql.acme": "Host=localhost;Database=acme;..."
      }
    }
  }
}
```

**PostgreSQL** (`DatabaseSecretsProvider`). A self-hosted vault: each secret is encrypted by
pgcrypto's `pgp_sym_encrypt` and stored in the table `vault.secrets`. The provider reads and writes.
You operate the database, and you keep the passphrases outside it. See the next section.

**Scaleway** (`ScalewaySecretsProvider`). Reads from Scaleway Secret Manager, a managed vault. The
provider is read-only: secrets are created with Scaleway's tools. Register it with
`AddScalewaySecretsProvider(builder.Configuration)`; it reads `ProjectId`, `ApiToken` and `Region`
from the section `Stellaegis:Secrets:Scaleway`.

**Remote** (`Stellaegis.Secrets.Client` and `Stellaegis.Secrets.AspNetCore`). One process hosts a
provider and exposes it over HTTP; other processes use it through the client, which implements the
same interface. Your code does not change between the in-process and the remote setups — only the
registration at startup does. The cost is one network call per read, and a host to secure.

## The PostgreSQL vault

Create the schema with [`docs/sql/stellaegis_secrets.sql`](https://github.com/Stellaegis/Stellaegis.Secrets/blob/main/docs/sql/stellaegis_secrets.sql), in the
database of your choice. The script requires the pgcrypto extension and creates it if it is missing.
[`docs/sql/reset-vault.sql`](https://github.com/Stellaegis/Stellaegis.Secrets/blob/main/docs/sql/reset-vault.sql) deletes every secret, for development only.

```csharp
builder.Services.AddDatabaseSecretsProvider(builder.Configuration);
```

```json
{
  "Stellaegis": {
    "Secrets": {
      "ConnectionString": "Host=localhost;Database=vault;...",
      "ActiveKeySlot": 1,
      "Keys": { "1": "set from an environment variable, never committed" }
    }
  }
}
```

The options are read from the section `Stellaegis:Secrets`; pass `sectionName` to read another one.
To set an option from code instead — a connection string kept elsewhere, for instance — follow the
registration with `services.PostConfigure<DatabaseSecretsOptions>(o => ...)`. The Scaleway provider
and the HTTP client follow the same pattern.

### Key slots

The provider holds two passphrases, in slots 1 and 2. New writes are encrypted with the active
slot's passphrase, and each row records the slot it was encrypted with. Reads use whichever slot the
row names.

To change passphrase, put the new one in the inactive slot and make that slot active. Secrets
written from then on use the new passphrase, and older secrets still read with the old one.

⚠ The library does not yet re-encrypt existing secrets with the active slot. Until it does, the old
passphrase must stay configured as long as a row uses it. You can see which rows still do with
`SELECT path FROM vault.secrets WHERE key_slot = <old slot>`.

## Exposing a vault over HTTP

The endpoints return secret values in clear, so their access is always declared explicitly. Name
the authorization policy that protects them:

```csharp
builder.Services.AddDatabaseSecretsProvider(builder.Configuration);
builder.Services.AddAuthentication(/* your scheme */);
builder.Services.AddAuthorization(o => o.AddPolicy("vault", p => p.RequireAuthenticatedUser()));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGroup("/api/vault").MapSecretsEndpoints(authorizationPolicy: "vault");
```

or, on a host that only a trusted network can reach, open them deliberately:

```csharp
app.MapGroup("/api/vault").MapSecretsEndpointsAnonymous();
```

Three endpoints are mapped under the group: `POST secrets/get`, `secrets/set` and `secrets/delete`.
The write endpoints answer `501 Not Implemented` when the registered provider is read-only.

Everything else about the host is yours to configure: the authentication scheme, CORS, rate
limiting, forwarded headers, health checks. The library makes no assumption about how your services
authenticate one another.

On the calling side, register the client and attach the authentication your host expects as a
`DelegatingHandler`:

```csharp
builder.Services.AddStellaegisHttpVaultClient(builder.Configuration)
    .AddHttpMessageHandler<MyTokenHandler>();
```

```json
{
  "Stellaegis": {
    "Secrets": {
      "Client": { "BaseUrl": "https://vault.example.com/api/vault/" }
    }
  }
}
```

## Being notified of changes

The providers publish no events. To react when a secret changes — for instance to evict a cached
connection string — decorate `IMutableSecretsProvider`: forward each call to the inner provider,
then publish on the transport you already use.

## Building and testing

```sh
dotnet test --solution Stellaegis.Secrets.slnx
```

The PostgreSQL tests need a server. By default they connect to `127.0.0.1` as `postgres`, and create
a database named `stellaegis_secrets_tests` if it does not exist. Set
`STELLAEGIS_SECRETS_TEST_POSTGRES` to another connection string to change that. When no server
answers, these tests are skipped and the others still run.

## License

MIT — see [LICENSE](https://github.com/Stellaegis/Stellaegis.Secrets/blob/main/LICENSE). The packages depend on third-party components listed in
[THIRD-PARTY-NOTICES.md](https://github.com/Stellaegis/Stellaegis.Secrets/blob/main/THIRD-PARTY-NOTICES.md).
