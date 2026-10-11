namespace Stellaegis.Secrets.Scaleway;

/// <summary>
/// Native names of Scaleway Secret Manager: every segment but the last is a folder, the last is the
/// secret's name. <c>tenantDB.primary.postgresql.acme</c> is the secret <c>acme</c> in the folder
/// <c>/tenantDB/primary/postgresql</c>, written as one string <c>/tenantDB/primary/postgresql/acme</c>.
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
        if (!nativeName.StartsWith('/') || !segments.All(SecretPath.IsValidSegment))
            throw new FormatException($"'{nativeName}' is not a Scaleway secret name of the form /folder/.../name.");

        return SecretPath.From(segments);
    }

    /// <summary>
    /// The folder and the name Scaleway's API takes for <paramref name="path"/>: <c>/a/b/c</c> and
    /// <c>d</c> for <c>a.b.c.d</c>, <c>/</c> and <c>a</c> for a single segment.
    /// </summary>
    internal static (string Folder, string Name) FolderAndName(SecretPath path)
    {
        var segments = path.Segments;
        var folder   = "/" + string.Join('/', segments.Take(segments.Count - 1));
        return (folder, segments[^1]);
    }
}
