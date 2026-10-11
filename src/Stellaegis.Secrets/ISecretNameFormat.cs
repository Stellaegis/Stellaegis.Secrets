namespace Stellaegis.Secrets;

/// <summary>
/// Translates a <see cref="SecretPath"/> to the native name of a vault, and back.
/// </summary>
/// <remarks>
/// <para>
/// Each adapter exposes its own format. It serves outside the adapter too: to find a secret in the
/// vault's console from a path, or to migrate secrets from one vault to another — <see cref="Split"/>
/// on the names read from the first, <see cref="Join"/> for the second.
/// </para>
/// <para>
/// A format is injective: <c>Split(Join(path))</c> equals <c>path</c> for every path it accepts. A
/// format that cannot represent a path refuses it rather than map two paths to one name.
/// </para>
/// </remarks>
public interface ISecretNameFormat
{
    /// <summary>The vault's native name for <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentException">The vault cannot represent <paramref name="path"/>.</exception>
    string Join(SecretPath path);

    /// <summary>The path a native name stands for.</summary>
    /// <exception cref="FormatException"><paramref name="nativeName"/> does not stand for a secret path.</exception>
    SecretPath Split(string nativeName);
}
