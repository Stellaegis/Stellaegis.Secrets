namespace Stellaegis.Secrets.AspNetCore;

// The wire of the vault API, shared by name with Stellaegis.Secrets.Client: paths travel in their text form.

/// <summary>Reads one secret, answered 404 with the code <c>Secret.NotFound</c> when it does not exist.</summary>
internal sealed record ReadSecretRequest(SecretPath? Path);

/// <summary>Reads several secrets; a path with no secret is absent from the answer.</summary>
internal sealed record GetSecretsRequest(IReadOnlyList<SecretPath>? Paths);

/// <summary>Creates or replaces a secret.</summary>
internal sealed record SetSecretRequest(SecretPath? Path, string? Value);

/// <summary>Removes a secret.</summary>
internal sealed record DeleteSecretRequest(SecretPath? Path);
