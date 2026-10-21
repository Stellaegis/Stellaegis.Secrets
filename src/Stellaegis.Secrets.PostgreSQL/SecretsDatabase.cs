using Stellaegis.Primitives.Data;

namespace Stellaegis.Secrets.PostgreSQL;

/// <summary>
/// What the PostgreSQL provider declares about its database: the schema <c>vault</c>, its numbered scripts, the
/// pgcrypto extension. An installer applies it under the role that owns the schema
/// (<see cref="DatabaseLifecycle.InstallAsync"/>); the application verifies it at startup under its own role
/// (<see cref="DatabaseLifecycle.VerifyAsync"/>, registered by <c>AddDatabaseSecretsProvider</c>).
/// </summary>
public static class SecretsDatabase
{
    /// <summary>The schema holding the vault, <c>vault</c>.</summary>
    public const string Schema = "vault";

    /// <summary>The declaration of the vault's database.</summary>
    public static ModuleDatabase Value { get; } = ModuleDatabase.PostgreSql<SecretsModule>(Schema)
        .WithScripts(SqlScripts.FromResources(typeof(SecretsDatabase).Assembly, "Stellaegis.Secrets.PostgreSQL.Sql.PostgreSql"))
        .RequiringExtension("pgcrypto");
}
