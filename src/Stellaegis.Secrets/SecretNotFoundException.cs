using Stellaegis.Primitives;

namespace Stellaegis.Secrets;

/// <summary>
/// No secret exists at the path asked for. Part of the contract of every provider: the caller recognizes it by
/// its code, <see cref="Code"/>, in process as over HTTP, where the host answers it as a 404 and the client raises
/// it again.
/// </summary>
public sealed class SecretNotFoundException : UserFacingException
{
    /// <summary>The code of the error, <c>Secret.NotFound</c>.</summary>
    public const string Code = "Secret.NotFound";

    /// <summary>Creates the exception for <paramref name="path"/>, which the caller supplied.</summary>
    public SecretNotFoundException(SecretPath path)
        : base(Error.NotFound(Code, $"No secret exists at {Guard.NotNull(path)}."))
    {
        Path = path;
    }

    /// <summary>Creates the exception from the error a host answered, when the client reads it back.</summary>
    public SecretNotFoundException(Error error)
        : base(Guard.NotNull(error))
    {
    }

    /// <summary>The path asked for, when known.</summary>
    public SecretPath? Path { get; }
}
