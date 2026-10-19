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
| `Stellaegis.Secrets` | The interfaces, `SecretPath`, and two providers for development: in memory and from configuration | `Stellaegis.Primitives`, Microsoft.Extensions abstractions |
| `Stellaegis.Secrets.PostgreSQL` | A self-hosted vault: secrets encrypted by pgcrypto in a PostgreSQL table | Npgsql, Dapper |
| `Stellaegis.Secrets.Scaleway` | A read-only provider for Scaleway Secret Manager | `Microsoft.Extensions.Http` |
| `Stellaegis.Secrets.AspNetCore` | Endpoints that expose a provider over HTTP | ASP.NET Core |
| `Stellaegis.Secrets.Client` | A provider that calls those endpoints from another process | `Microsoft.Extensions.Http` |

## Identifying a secret: `SecretPath`

A secret is identified by a `SecretPath`. A path has a fixed shape: the space the secret belongs to,
then the module that gives the secret its meaning, then one or more names that this module chooses.

```text
{space}.{module}.{name}…      e.g.  acme.billing.smtp.password
```

You build a path from a space and a module type:

```csharp
public sealed class BillingModule : IStellaegisModule
{
    public static string Name => "billing";
}

var acme     = Compartment.Parse("acme");
var password = SecretPath.For<BillingModule>(acme, "smtp", "password");   // acme.billing.smtp.password
var fallback = SecretPath.For<BillingModule>(Space.Shared, "smtp");       // _shared.billing.smtp
var apiKey   = SecretPath.ForAssembly<BillingModule>("api-key");          // _assembly.billing.api-key
var same     = SecretPath.Parse("acme.billing.smtp.password");
```

`Compartment`, `Space` and `IStellaegisModule` come from
[Stellaegis.Primitives](https://github.com/Stellaegis/Stellaegis.Primitives), which this package
depends on. Its core has no dependency other than .NET.

### The space

The first segment says where the secret belongs. It takes one of three forms.

- **A compartment**, such as `acme`. A compartment is the boundary your application isolates its data
  by: a customer, an organization, a tenant. A secret of `acme` serves only `acme`'s data, for
  instance the connection string of its database or the credentials of its mail server.
- **`_shared`**, the shared space. Its secrets serve every compartment, for instance a default mail
  server.
- **`_assembly`**, for a secret of the application itself, which belongs to no space: the connection
  string of the database your application runs on, or the API key of a service it calls. The word
  refers to the application as assembled from its modules and adapters, not to a .NET assembly.

A compartment name never starts with `_`, so no compartment can reach the shared space or the
application's own secrets. An application without compartments only builds paths with
`ForAssembly`, and never handles a space.

Every secret of a compartment lies under one prefix, `acme.`, which `SecretPath.TextPrefixOf` gives.
A vault can therefore find every secret of a compartment, and remove them all when the compartment
goes away.

### The module

The second segment is the name of the module that writes and reads the secret: only that module
knows what the secret means. A module is a type implementing `IStellaegisModule`, whose name is 3 to
63 lowercase letters and digits, starting with a letter. Passing the type rather than a string
writes the name in one place. Your application keeps its own secrets under a module that it declares
for itself in the same way.

Each module writes under its own name, so two modules with distinct names never write over each
other's secrets.

### The names

The segments after the module belong to it. There is at least one. Each is non-empty and uses only
letters, digits, `_` and `-`. No segment ever contains a dot, so the text form — produced by
`ToString()` and read by `Parse` — cannot be ambiguous. That text form is convenient in
configuration files and logs. `Parse` refuses a text whose first segment is not a space or whose
second is not a module name.

### Why a fixed shape

A fixed shape costs freedom: you cannot organise paths your own way, and each path names a module
type. In return, the library knows where the space of every secret is. With paths shaped freely, each
part of an application would place the compartment where it sees fit, and a secret written under
another convention would escape the removal of its compartment without any error.

### Native names: `ISecretNameFormat`

Vaults name their secrets in different ways. HashiCorp Vault and Scaleway organise them in folders
separated by `/`. Azure Key Vault and Google Secret Manager use flat names with a restricted set of
characters. A path made of segments lets each adapter build the name its vault expects, so your code
never handles a separator.

Each adapter translates a path into its vault's native name through an `ISecretNameFormat`. The
format is public, so that you can find a secret in the vault's console from its path, or move
secrets from one vault to another.

| Provider | Native name of `acme.billing.smtp.password` |
| --- | --- |
| Configuration, PostgreSQL | `acme.billing.smtp.password` |
| Scaleway | the secret `password` in the folder `/acme/billing/smtp` |

In a vault with folders, each compartment is a top-level folder.

`Join` turns a path into a native name, and `Split` turns a native name back into a path.
`Split(Join(path))` always returns the original path. A format that cannot represent a path refuses
it with an error, rather than give two different paths the same name.

To migrate between two vaults, read the native names from the first one, convert each of them with
the first format's `Split`, then write the secrets to the second vault, which applies its own format.

Flat vaults restrict the characters of a name. Azure Key Vault, for instance, accepts only letters,
digits and `-`. A format joining segments with `-` could represent neither a compartment whose name
contains `-` nor the segments `_shared` and `_assembly`. An adapter for such a vault would have to
escape these characters, and the library provides no such adapter today.

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
        "acme.billing.database": "Host=localhost;Database=acme;..."
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

### Where the keys live

A provider running in your application's process holds what opens the vault. For the PostgreSQL
provider, that is the connection string to the table and the passphrases. Any code that runs with
these settings can read every secret of every compartment, with or without this library: `psql` and
`pgp_sym_decrypt` are enough. The library cannot prevent it. What protects the vault is where these
settings live.

A vault exposed over HTTP keeps them on its own host. Your applications then hold only the credential
they call the vault with, which you can revoke and trace caller by caller, and the host can be
reachable from your own services only. In return, the HTTP endpoints apply one authorization policy
to every path today: a caller allowed to read reads every secret, whatever its space or module.
Authorizing each call by the space and the module of the path it asks for is not available yet.

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

The options are checked when the host starts. A missing connection string, an active slot other than
1 or 2, or no passphrase for the active slot stops the start with an `OptionsValidationException`
that names the setting. The Scaleway provider checks `ProjectId` and `ApiToken` the same way. An
application without a .NET host, such as a desktop application, gets the same exception the first
time it resolves the provider.

### Key slots

The provider holds two passphrases, in slots 1 and 2. New writes are encrypted with the active
slot's passphrase, and each row records the slot it was encrypted with. Reads use whichever slot the
row names.

To change passphrase, put the new one in the inactive slot and make that slot active. Secrets
written from then on use the new passphrase, and older secrets still read with the old one.

⚠ The library does not yet re-encrypt existing secrets with the active slot. Until it does, the old
passphrase must stay configured as long as a row uses it. You can see which rows still do with
`SELECT path FROM vault.secrets WHERE key_slot = <old slot>`.

### Purging a compartment

When a compartment goes away — a customer leaves, for instance — its secrets go with it.
`ISecretsPurge.PurgeCompartmentAsync(compartment, actor)` deletes every secret under the
compartment's prefix, whatever module wrote it, and answers how many it deleted. The shared space and
the application's own secrets stay. A replayed purge completes an interrupted one, and answers `0`
once nothing is left. The purge publishes no event: it logs its actor when it starts and when it
ends.

```csharp
var purged = await purge.PurgeCompartmentAsync(Compartment.Parse("acme"), Actor.Parse("job:purge"));
```

`AddDatabaseSecretsProvider` registers `ISecretsPurge` next to the other two interfaces, and
`InMemorySecretsProvider` implements it too. The purge has an interface of its own, apart from
`IMutableSecretsProvider`. Every module writes its secrets through `IMutableSecretsProvider`, while
only the code in charge of administration takes `ISecretsPurge`: a module that keeps its own secrets
cannot erase those of the others.

The purge runs in the process that holds the provider. It has no HTTP endpoint.

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
The write endpoints answer `501 Not Implemented` when the registered provider is read-only. The
endpoints carry no route name, so you can map them more than once in one host.

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
