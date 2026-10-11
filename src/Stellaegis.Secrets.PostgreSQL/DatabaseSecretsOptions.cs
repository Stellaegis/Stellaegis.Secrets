namespace Stellaegis.Secrets.PostgreSQL;

/// <summary>
/// Options for <see cref="DatabaseSecretsProvider"/> — a PostgreSQL-backed vault using pgcrypto encryption.
/// </summary>
/// <remarks>
/// Key rotation uses a modulo-2 double-buffer pattern: two named slots (1 and 2) alternate as the
/// active slot. Only the two current passphrases need to be in memory at any time.
/// <para>
/// Configuration (rotation-ready):
/// <code>
/// Stellaegis:Secrets:ActiveKeySlot = 1
/// Stellaegis:Secrets:Keys:1        = passphrase-for-slot-1
/// Stellaegis:Secrets:Keys:2        = passphrase-for-slot-2  (empty until first rotation)
/// </code>
/// </para>
/// <para>
/// Legacy (single passphrase, treated as slot 1):
/// <code>
/// Stellaegis:Secrets:Passphrase = passphrase
/// </code>
/// </para>
/// </remarks>
public sealed class DatabaseSecretsOptions
{
    /// <summary>Connection string to the PostgreSQL database hosting the vault table.</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// Named passphrases indexed by slot number (1 or 2).
    /// Maps to <c>Stellaegis:Secrets:Keys:1</c> and <c>Stellaegis:Secrets:Keys:2</c>.
    /// </summary>
    public Dictionary<int, string> Keys { get; set; } = [];

    /// <summary>
    /// Slot currently used for new writes (1 or 2).
    /// Switch this to the new slot to activate a rotation.
    /// </summary>
    public int ActiveKeySlot { get; set; } = 1;

    /// <summary>
    /// Legacy single passphrase — treated as <c>Keys[1]</c> when <see cref="Keys"/> is empty.
    /// Should be sourced from an environment variable, never hardcoded.
    /// </summary>
    public string Passphrase { get; set; } = "";

    /// <summary>
    /// Returns the passphrase for the given slot.
    /// <list type="bullet">
    ///   <item>Active slot: throws if not configured (startup guard).</item>
    ///   <item>Non-active slot: returns empty string when not configured — safe as long as
    ///     no row in the vault uses that slot. If a row does, pgcrypto raises a decryption error.</item>
    /// </list>
    /// Falls back to <see cref="Passphrase"/> (slot 1) for legacy single-passphrase setups.
    /// </summary>
    internal string GetPassphrase(int slot)
    {
        if (Keys.TryGetValue(slot, out var key) && !string.IsNullOrWhiteSpace(key))
            return key;

        // Legacy fallback: bare Passphrase maps to slot 1
        if (slot == 1 && !string.IsNullOrWhiteSpace(Passphrase))
            return Passphrase;

        // Non-active slots may not be configured yet (e.g. slot 2 before the first rotation).
        // Return empty — the CASE expression in SQL short-circuits and never calls
        // pgp_sym_decrypt with this value as long as no row uses that slot.
        if (slot != ActiveKeySlot)
            return string.Empty;

        throw new InvalidOperationException(
            $"No passphrase configured for active key slot {slot}. " +
            $"Set Stellaegis:Secrets:Keys:{slot} or Stellaegis:Secrets:Passphrase.");
    }

    /// <summary>Passphrase for the currently active slot — used for all new writes.</summary>
    internal string ActivePassphrase => GetPassphrase(ActiveKeySlot);
}
