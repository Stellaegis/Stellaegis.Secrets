using Stellaegis.Primitives;
using Stellaegis.Primitives.Hosting;

namespace Stellaegis.Secrets.PostgreSQL;

/// <summary>
/// Options for <see cref="DatabaseSecretsProvider"/> — a PostgreSQL-backed vault using pgcrypto encryption.
/// </summary>
/// <remarks>
/// The passphrases rotate by slots, the shape of <see cref="KeySlots"/>: two slots, 1 and 2, one active for new
/// writes, each row recording the slot that encrypted it.
/// <code>
/// Stellaegis:Secrets:ConnectionString = Host=...;Database=...
/// Stellaegis:Secrets:ActiveKeySlot = 1
/// Stellaegis:Secrets:Keys:1        = passphrase-for-slot-1
/// Stellaegis:Secrets:Keys:2        = passphrase-for-slot-2  (empty until first rotation)
/// </code>
/// Validated when the host starts: a missing connection string or an active slot without a passphrase stops the
/// start, naming the setting.
/// </remarks>
public sealed class DatabaseSecretsOptions : IValidatableOptions
{
    /// <summary>Default configuration section: <c>Stellaegis:Secrets</c>.</summary>
    public const string SectionName = "Stellaegis:Secrets";

    /// <summary>Connection string to the PostgreSQL database hosting the vault table, under the application's role.</summary>
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

    /// <summary>The passphrases as slots, the shape every rotating secret of the family shares.</summary>
    internal KeySlots Slots => new() { Keys = Keys, ActiveKeySlot = ActiveKeySlot };

    /// <summary>
    /// The passphrase of <paramref name="slot"/>: the active slot always has one; the other may be empty as long as
    /// no row uses it — the SQL <c>CASE</c> never decrypts with an unused slot's value.
    /// </summary>
    internal string GetPassphrase(int slot) => Slots.KeyOf(slot);

    /// <summary>Passphrase for the currently active slot — used for all new writes.</summary>
    internal string ActivePassphrase => Slots.ActiveKey;

    /// <inheritdoc/>
    public string? FindError()
        => string.IsNullOrWhiteSpace(ConnectionString) ? "ConnectionString is required." : Slots.FindError();
}
