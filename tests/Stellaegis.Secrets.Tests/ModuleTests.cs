using Microsoft.Extensions.DependencyInjection;
using Stellaegis.Primitives;
using Stellaegis.Primitives.Data;
using Stellaegis.Primitives.Hosting;
using Stellaegis.Secrets.PostgreSQL;

namespace Stellaegis.Secrets.Tests;

/// <summary>What the module declares, read against the frames an application may have.</summary>
public sealed class ModuleTests
{
    [Fact]
    public void The_module_fits_an_application_with_or_without_compartments()
    {
        ApplicationFrame.WithoutCompartments.Validate([SecretsModule.Declaration]);
        ApplicationFrame.SingleCompartment("acme").Validate([SecretsModule.Declaration]);
        ApplicationFrame.SingleCompartment("acme").WithOwnerIsolation().Validate([SecretsModule.Declaration]);
        ApplicationFrame.ManyCompartments().WithSharedSpace().Validate([SecretsModule.Declaration]);

        Assert.Equal(ModuleCompartments.Optional, SecretsModule.Declaration.Compartments);
        Assert.True(SecretsModule.Declaration.AcceptsSharedSpace);
        Assert.Equal(["secrets.read", "secrets.write", "secrets.delete", "secrets.purge"], SecretsModule.Declaration.Operations.Select(o => o.Identity));
    }

    [Fact]
    public void Every_registration_declares_the_module_once()
    {
        var services = new ServiceCollection().AddStellaegisSecrets().AddStellaegisSecrets();
        using var provider = services.BuildServiceProvider();

        var declared = Assert.Single(provider.GetStellaegisModules());
        Assert.Equal("secrets", declared.Name);
    }

    [Fact]
    public void The_vault_database_is_declared_with_its_script_and_its_extension()
    {
        Assert.Equal("vault", SecretsDatabase.Value.Schema);
        Assert.Equal(["0001_initial.sql"], SecretsDatabase.Value.Scripts.Select(s => s.FileName));
        Assert.Equal(["pgcrypto"], SecretsDatabase.Value.Extensions);
        Assert.Contains("create table vault.secrets", SecretsDatabase.Value.Scripts[0].Sql);
        Assert.DoesNotContain("create extension", SecretsDatabase.Value.Scripts[0].Sql);
    }
}
