namespace Stellaegis.Secrets;

/// <summary>
/// Native names in the text form of a path: segments joined by <c>.</c>, as in
/// <c>acme.billing.smtp.password</c>. Used by the configuration and PostgreSQL providers.
/// </summary>
public sealed class DottedSecretNameFormat : ISecretNameFormat
{
    /// <summary>The single instance — the format holds no state.</summary>
    public static DottedSecretNameFormat Instance { get; } = new();

    private DottedSecretNameFormat() { }

    /// <inheritdoc />
    public string Join(SecretPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.ToString();
    }

    /// <inheritdoc />
    public SecretPath Split(string nativeName) => SecretPath.Parse(nativeName);
}
