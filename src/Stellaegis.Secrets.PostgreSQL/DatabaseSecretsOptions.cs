using Microsoft.Extensions.Options;

namespace Stellaegis.Secrets.PostgreSQL;

/// <summary>
/// Options for <see cref="DatabaseSecretsProvider"/> — a PostgreSQL-backed vault using pgcrypto encryption.
/// </summary>
/// <remarks>
/// Key rotation uses a modulo-2 double-buffer pattern: two named slots (1 and 2) alternate as the
/// active slot. Only the two current passphrases need to be in memory at any time.
/// <code>
/// Stellaegis:Secrets:ConnectionString = Host=...;Database=...
/// Stellaegis:Secrets:ActiveKeySlot = 1
/// Stellaegis:Secrets:Keys:1        = passphrase-for-slot-1
/// Stellaegis:Secrets:Keys:2        = passphrase-for-slot-2  (empty until first rotation)
/// </code>
/// </remarks>
public sealed class DatabaseSecretsOptions
{
    /// <summary>Default configuration section: <c>Stellaegis:Secrets</c>.</summary>
    public const string SectionName = "Stellaegis:Secrets";

    /// <summary>Connection string to the PostgreSQL database hosting the vault table.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Named passphrases indexed by slot number (1 or 2).
    /// Maps to <c>Stellaegis:Secrets:Keys:1</c> and <c>Stellaegis:Secrets:Keys:2</c>.
    /// Should be sourced from an environment variable, never hardcoded.
    /// </summary>
    public Dictionary<int, string> Keys { get; set; } = [];

    /// <summary>
    /// Slot currently used for new writes (1 or 2).
    /// Switch this to the new slot to activate a rotation.
    /// </summary>
    public int ActiveKeySlot { get; set; } = 1;

    /// <summary>
    /// Returns the passphrase for the given slot.
    /// <list type="bullet">
    ///   <item>Active slot: throws if not configured.</item>
    ///   <item>Non-active slot: returns empty string when not configured — safe as long as
    ///     no row in the vault uses that slot. If a row does, pgcrypto raises a decryption error.</item>
    /// </list>
    /// </summary>
    internal string GetPassphrase(int slot)
    {
        if (Keys.TryGetValue(slot, out var key) && !string.IsNullOrWhiteSpace(key))
            return key;

        // Non-active slots may not be configured yet (e.g. slot 2 before the first rotation).
        // Return empty — the CASE expression in SQL short-circuits and never calls
        // pgp_sym_decrypt with this value as long as no row uses that slot.
        if (slot != ActiveKeySlot)
            return string.Empty;

        throw new InvalidOperationException(MissingActiveKey());
    }

    /// <summary>Passphrase for the currently active slot — used for all new writes.</summary>
    internal string ActivePassphrase => GetPassphrase(ActiveKeySlot);

    /// <summary>The first setting that keeps the provider from working, or <see langword="null"/>.</summary>
    internal string? FindError()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            return "ConnectionString is required.";

        if (ActiveKeySlot is not (1 or 2))
            return $"ActiveKeySlot is {ActiveKeySlot}: it must be 1 or 2.";

        if (!Keys.TryGetValue(ActiveKeySlot, out var key) || string.IsNullOrWhiteSpace(key))
            return MissingActiveKey();

        return null;
    }

    private string MissingActiveKey() =>
        $"No passphrase configured for active key slot {ActiveKeySlot}: set Keys:{ActiveKeySlot} in the section of the options.";
}

/// <summary>
/// Validates <see cref="DatabaseSecretsOptions"/> when they are built — at host start, with <c>ValidateOnStart</c>.
/// </summary>
internal sealed class DatabaseSecretsOptionsValidator : IValidateOptions<DatabaseSecretsOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseSecretsOptions options) =>
        options.FindError() is { } error ? ValidateOptionsResult.Fail(error) : ValidateOptionsResult.Success;
}
