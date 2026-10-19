namespace Stellaegis.Secrets.Scaleway;

/// <summary>
/// Native names of Scaleway Secret Manager: every segment but the last is a folder, the last is the
/// secret's name. <c>acme.billing.smtp.password</c> is the secret <c>password</c> in the folder
/// <c>/acme/billing/smtp</c>, written as one string <c>/acme/billing/smtp/password</c>. Each space is a folder.
/// </summary>
public sealed class ScalewaySecretNameFormat : ISecretNameFormat
{
    /// <summary>The single instance — the format holds no state.</summary>
    public static ScalewaySecretNameFormat Instance { get; } = new();

    private ScalewaySecretNameFormat() { }

    /// <inheritdoc />
    public string Join(SecretPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return "/" + string.Join('/', path.Segments);
    }

    /// <inheritdoc />
    public SecretPath Split(string nativeName)
    {
        ArgumentNullException.ThrowIfNull(nativeName);

        var segments = nativeName.TrimStart('/').Split('/');
        if (!nativeName.StartsWith('/')
            || !segments.All(SecretPath.IsValidSegment)
            || !SecretPath.TryParse(string.Join(SecretPath.TextSeparator, segments), out var path))
        {
            throw new FormatException($"'{nativeName}' is not a Scaleway secret name of the form /space/module/.../name.");
        }

        return path;
    }

    /// <summary>
    /// The folder and the name Scaleway's API takes for <paramref name="path"/>: <c>/a/b/c</c> and
    /// <c>d</c> for <c>a.b.c.d</c>.
    /// </summary>
    internal static (string Folder, string Name) FolderAndName(SecretPath path)
    {
        var segments = path.Segments;
        var folder   = "/" + string.Join('/', segments.Take(segments.Count - 1));
        return (folder, segments[^1]);
    }
}
