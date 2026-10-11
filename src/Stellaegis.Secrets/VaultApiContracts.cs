namespace Stellaegis.Secrets;

/// <summary>
/// Request model for reading one or more secrets from the Vault API.
/// Shared contract between the Vault API (server) and its callers (client).
/// </summary>
public sealed record GetSecretsRequest(IReadOnlyList<string> Paths);

/// <summary>
/// Request model for creating or replacing a secret via the Vault API.
/// Shared contract between the Vault API (server) and its callers (client).
/// </summary>
public sealed record SetSecretRequest(string Path, string Value);

/// <summary>
/// Request model for removing a secret via the Vault API.
/// Shared contract between the Vault API (server) and its callers (client).
/// </summary>
public sealed record DeleteSecretRequest(string Path);
