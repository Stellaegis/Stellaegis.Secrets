using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Stellaegis.Secrets;

/// <summary>
/// Identifies a secret as a sequence of segments, independently of any vault's naming syntax.
/// </summary>
/// <remarks>
/// <para>
/// Each adapter translates a path into its vault's native name through its
/// <see cref="ISecretNameFormat"/> — folders and a name for a hierarchical vault, a flat name for
/// another. Callers never handle a separator.
/// </para>
/// <para>
/// The module gives segments no meaning. A grammar such as <c>{usage}.{name}.{provider}.{compartment}</c>
/// (e.g. <c>tenantDB.primary.postgresql.acme</c>) is advice, not a constraint.
/// </para>
/// <para>
/// Every segment is non-empty and made of <c>[A-Za-z0-9_-]</c>. Since no segment holds a dot, the
/// text form <c>a.b.c</c> (<see cref="ToString"/>, <see cref="Parse"/>) is unambiguous.
/// </para>
/// </remarks>
[JsonConverter(typeof(SecretPathJsonConverter))]
public sealed class SecretPath : IEquatable<SecretPath>
{
    /// <summary>The character separating segments in the text form.</summary>
    public const char TextSeparator = '.';

    private readonly string[] _segments;
    private readonly string _text;

    private SecretPath(string[] segments)
    {
        _segments = segments;
        _text     = string.Join(TextSeparator, segments);
    }

    /// <summary>The segments of the path, in order. Never empty.</summary>
    public IReadOnlyList<string> Segments => _segments;

    /// <summary>
    /// Creates a path from its segments.
    /// </summary>
    /// <exception cref="ArgumentException">No segment is given, or one is not a valid segment.</exception>
    public static SecretPath From(params IEnumerable<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var array = segments.ToArray();
        if (array.Length == 0)
            throw new ArgumentException("A secret path has at least one segment.", nameof(segments));

        foreach (var segment in array)
        {
            if (!IsValidSegment(segment))
                throw new ArgumentException(
                    $"Invalid secret path segment '{segment}': segments are non-empty and made of [A-Za-z0-9_-].",
                    nameof(segments));
        }

        return new SecretPath(array);
    }

    /// <summary>
    /// Parses the text form <c>a.b.c</c> — for configuration and input, never needed by the port.
    /// </summary>
    /// <exception cref="FormatException"><paramref name="text"/> is not a valid secret path.</exception>
    public static SecretPath Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return TryParse(text, out var path)
            ? path
            : throw new FormatException(
                $"Invalid secret path '{text}': expected segments made of [A-Za-z0-9_-], separated by '{TextSeparator}'.");
    }

    /// <summary>
    /// Parses the text form <c>a.b.c</c>, without throwing.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out SecretPath? path)
    {
        path = null;
        if (string.IsNullOrEmpty(text))
            return false;

        var segments = text.Split(TextSeparator);
        if (!segments.All(IsValidSegment))
            return false;

        path = new SecretPath(segments);
        return true;
    }

    /// <summary>
    /// Whether <paramref name="segment"/> is a valid segment: non-empty, made of <c>[A-Za-z0-9_-]</c>.
    /// </summary>
    public static bool IsValidSegment([NotNullWhen(true)] string? segment) =>
        !string.IsNullOrEmpty(segment) && segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    /// <summary>The text form: segments joined by <see cref="TextSeparator"/>.</summary>
    public override string ToString() => _text;

    /// <inheritdoc />
    public bool Equals(SecretPath? other) =>
        other is not null && string.Equals(_text, other._text, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as SecretPath);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_text);

    /// <summary>Whether two paths have the same segments.</summary>
    public static bool operator ==(SecretPath? left, SecretPath? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Whether two paths differ.</summary>
    public static bool operator !=(SecretPath? left, SecretPath? right) => !(left == right);
}
