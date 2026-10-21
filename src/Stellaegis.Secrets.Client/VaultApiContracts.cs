namespace Stellaegis.Secrets.Client;

// The wire of the vault API, shared by name with Stellaegis.Secrets.AspNetCore: paths travel in their text form.

internal sealed record ReadSecretRequest(SecretPath Path);

internal sealed record GetSecretsRequest(IReadOnlyList<SecretPath> Paths);

internal sealed record SetSecretRequest(SecretPath Path, string Value);

internal sealed record DeleteSecretRequest(SecretPath Path);
