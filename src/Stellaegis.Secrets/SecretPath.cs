using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Stellaegis.Primitives;

namespace Stellaegis.Secrets;

/// <summary>
/// Identifies a secret, independently of any vault's naming syntax: the space it belongs to, the module that
/// gives it its meaning, then names of the module's own choosing — <c>{space}.{module}.{name}…</c>.
/// </summary>
/// <remarks>
/// <para>
/// The first segment is the space: a compartment (<c>acme</c>), <see cref="Space.SharedSegment"/> for the shared
/// space, or <see cref="AssemblySegment"/> for a secret of the application itself, which belongs to no space — a
/// connection string, an API key of an adapter the application assembles. Neither marker is a valid compartment,
/// so no compartment ever reaches them. Everything a compartment holds sits under one prefix, which is what lets a
/// vault purge a compartment.
/// </para>
/// <para>
/// The second segment is the name of the module that writes and reads the secret, an
/// <see cref="IStellaegisModule"/>. An application keeps its own secrets under a module it declares for itself.
/// Two modules cannot share a name, so two packages never write under the same prefix.
/// </para>
/// <para>
/// The names that follow belong to the module, which alone knows their meaning: at least one, each non-empty and
/// made of <c>[A-Za-z0-9_-]</c>. Since no segment holds a dot, the text form <c>a.b.c</c> (<see cref="ToString"/>,
/// <see cref="Parse"/>) is unambiguous.
/// </para>
/// <para>
/// Each adapter translates a path into its vault's native name through its <see cref="ISecretNameFormat"/> —
/// folders and a name for a hierarchical vault, a flat name for another. Callers never handle a separator.
/// </para>
/// </remarks>
[JsonConverter(typeof(SecretPathJsonConverter))]
public sealed class SecretPath : IEquatable<SecretPath>
{
    /// <summary>The character separating segments in the text form.</summary>
    public const char TextSeparator = '.';

    /// <summary>
    /// The first segment of a secret that belongs to no space: a secret of the application itself.
    /// </summary>
    public const string AssemblySegment = "_assembly";

    private readonly string[] _segments;
    private readonly string _text;

    private SecretPath(string[] segments)
    {
        _segments = segments;
        _text     = string.Join(TextSeparator, segments);
    }

    /// <summary>
    /// The segments of the path, in order: the space, the module, then at least one name.
    /// </summary>
    public IReadOnlyList<string> Segments => _segments;

    /// <summary>
    /// The path of a secret that <typeparamref name="TModule"/> keeps in <paramref name="space"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// No name is given, one is not a valid segment, or the module's name is not a valid module name.
    /// </exception>
    public static SecretPath For<TModule>(Space space, params IEnumerable<string> names)
        where TModule : IStellaegisModule =>
        Create<TModule>(Guard.NotNull(space).ToSegment(), names);

    /// <summary>
    /// The path of a secret that <typeparamref name="TModule"/> keeps for the application itself, outside every
    /// space — the only kind of path an application without compartments uses.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// No name is given, one is not a valid segment, or the module's name is not a valid module name.
    /// </exception>
    public static SecretPath ForAssembly<TModule>(params IEnumerable<string> names)
        where TModule : IStellaegisModule =>
        Create<TModule>(AssemblySegment, names);

    /// <summary>
    /// The prefix, in the text form, under which every secret of <paramref name="compartment"/> lies, separator
    /// included — <c>acme.</c>.
    /// </summary>
    public static string TextPrefixOf(Compartment compartment) => Guard.NotNull(compartment).Value + TextSeparator;

    /// <summary>
    /// Parses the text form <c>{space}.{module}.{name}…</c> — for configuration and input, never needed by the port.
    /// </summary>
    /// <exception cref="FormatException"><paramref name="text"/> is not a valid secret path.</exception>
    public static SecretPath Parse(string text)
    {
        Guard.NotNull(text);

        return TryParse(text, out var path)
            ? path
            : throw new FormatException(
                $"Invalid secret path '{text}': expected {{space}}.{{module}}.{{name}}…, the space a compartment, " +
                $"'{Space.SharedSegment}' or '{AssemblySegment}', the module a module name, each name made of " +
                $"[A-Za-z0-9_-].");
    }

    /// <summary>
    /// Parses the text form <c>{space}.{module}.{name}…</c>, without throwing.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out SecretPath? path)
    {
        path = null;
        if (string.IsNullOrEmpty(text))
            return false;

        var segments = text.Split(TextSeparator);
        if (segments.Length < 3
            || !IsSpaceSegment(segments[0])
            || !NameRules.IsModuleName(segments[1])
            || !segments.Skip(2).All(IsValidSegment))
        {
            return false;
        }

        path = new SecretPath(segments);
        return true;
    }

    /// <summary>
    /// Whether <paramref name="segment"/> is a valid name segment: non-empty, made of <c>[A-Za-z0-9_-]</c>.
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

    private static SecretPath Create<TModule>(string spaceSegment, IEnumerable<string> names)
        where TModule : IStellaegisModule
    {
        var module = NameRules.ModuleNameOf<TModule>();
        var array  = Guard.NotNull(names).ToArray();

        if (array.Length == 0)
            throw new ArgumentException("A secret path has at least one name after its module.", nameof(names));

        foreach (var name in array)
        {
            if (!IsValidSegment(name))
                throw new ArgumentException(
                    $"Invalid secret name '{name}': names are non-empty and made of [A-Za-z0-9_-].",
                    nameof(names));
        }

        return new SecretPath([spaceSegment, module, .. array]);
    }

    private static bool IsSpaceSegment(string segment) =>
        segment is Space.SharedSegment or AssemblySegment || Compartment.TryParse(segment, out _);
}
