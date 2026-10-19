using Stellaegis.Primitives;

namespace Stellaegis.Secrets.Tests;

/// <summary>A module that keeps secrets per space, as a compartmented module would.</summary>
public sealed class BillingModule : IStellaegisModule
{
    public static string Name => "billing";
}

/// <summary>A module of the application itself, keeping secrets outside every space.</summary>
public sealed class DesktopModule : IStellaegisModule
{
    public static string Name => "desktop";
}

/// <summary>A module whose name breaks the module name rule.</summary>
public sealed class InvalidModule : IStellaegisModule
{
    public static string Name => "in-valid";
}
