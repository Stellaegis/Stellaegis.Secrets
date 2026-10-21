using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Stellaegis.Primitives;
using Stellaegis.Secrets.PostgreSQL;

namespace Stellaegis.Secrets.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class DatabaseSecretsProviderTests(PostgreSqlFixture database)
{
    private const string Key1 = "passphrase-for-slot-1";
    private const string Key2 = "passphrase-for-slot-2";

    private DatabaseSecretsProvider Provider(int activeSlot, params (int Slot, string Key)[] keys)
    {
        Assert.SkipWhen(database.Unavailable is not null, database.Unavailable ?? "");

        var options = new DatabaseSecretsOptions
        {
            ConnectionString = database.ConnectionString,
            ActiveKeySlot    = activeSlot,
            Keys             = keys.ToDictionary(k => k.Slot, k => k.Key),
        };
        return new DatabaseSecretsProvider(Options.Create(options), NullLogger<DatabaseSecretsProvider>.Instance);
    }

    /// <summary>A path no other test writes: the tests share one table.</summary>
    private static SecretPath UniquePath() => SecretPath.ForAssembly<BillingModule>("test", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task A_written_secret_reads_back()
    {
        var provider = Provider(1, (1, Key1));
        var path     = UniquePath();
        var ct       = TestContext.Current.CancellationToken;

        await provider.SetSecretAsync(path, "{\"connectionString\":\"Host=x\"}", ct);

        Assert.Equal("{\"connectionString\":\"Host=x\"}", await provider.GetSecretAsync(path, ct));
    }

    [Fact]
    public async Task The_value_is_stored_encrypted()
    {
        var provider = Provider(1, (1, Key1));
        var path     = UniquePath();
        var ct       = TestContext.Current.CancellationToken;

        await provider.SetSecretAsync(path, "plain-value", ct);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var read = new NpgsqlCommand("SELECT secret_json FROM vault.secrets WHERE path = @path", connection);
        read.Parameters.AddWithValue("path", DatabaseSecretsProvider.NameFormat.Join(path));
        var stored = (byte[])(await read.ExecuteScalarAsync(ct))!;

        Assert.DoesNotContain("plain-value", System.Text.Encoding.UTF8.GetString(stored));
    }

    [Fact]
    public async Task A_write_replaces_the_previous_value()
    {
        var provider = Provider(1, (1, Key1));
        var path     = UniquePath();
        var ct       = TestContext.Current.CancellationToken;

        await provider.SetSecretAsync(path, "first", ct);
        await provider.SetSecretAsync(path, "second", ct);

        Assert.Equal("second", await provider.GetSecretAsync(path, ct));
    }

    [Fact]
    public async Task A_wrong_passphrase_fails_rather_than_returning_garbage()
    {
        var path = UniquePath();
        var ct   = TestContext.Current.CancellationToken;
        await Provider(1, (1, Key1)).SetSecretAsync(path, "value", ct);

        var wrong = Provider(1, (1, "not-the-passphrase"));

        await Assert.ThrowsAsync<PostgresException>(() => wrong.GetSecretAsync(path, ct));
    }

    [Fact]
    public async Task After_switching_slots_old_and_new_secrets_both_read()
    {
        var before = UniquePath();
        var after  = UniquePath();
        var ct     = TestContext.Current.CancellationToken;
        await Provider(1, (1, Key1)).SetSecretAsync(before, "written-with-slot-1", ct);

        var rotated = Provider(2, (1, Key1), (2, Key2));
        await rotated.SetSecretAsync(after, "written-with-slot-2", ct);

        Assert.Equal("written-with-slot-1", await rotated.GetSecretAsync(before, ct));
        Assert.Equal("written-with-slot-2", await rotated.GetSecretAsync(after, ct));

        var batch = await rotated.GetSecretsAsync([before, after], ct);
        Assert.Equal(2, batch.Count);
    }

    [Fact]
    public async Task A_secret_written_with_slot_2_needs_the_slot_2_passphrase()
    {
        var path = UniquePath();
        var ct   = TestContext.Current.CancellationToken;
        await Provider(2, (1, Key1), (2, Key2)).SetSecretAsync(path, "value", ct);

        var slot2Lost = Provider(1, (1, Key1), (2, "not-the-passphrase"));

        await Assert.ThrowsAsync<PostgresException>(() => slot2Lost.GetSecretAsync(path, ct));
    }

    [Fact]
    public async Task A_missing_secret_throws_on_single_read_and_is_absent_from_batch_read()
    {
        var provider = Provider(1, (1, Key1));
        var present  = UniquePath();
        var missing  = UniquePath();
        var ct       = TestContext.Current.CancellationToken;
        await provider.SetSecretAsync(present, "value", ct);

        var error = await Assert.ThrowsAsync<SecretNotFoundException>(() => provider.GetSecretAsync(missing, ct));
        Assert.Equal(missing, error.Path);

        var batch = await provider.GetSecretsAsync([present, missing], ct);
        Assert.Equal(new Dictionary<SecretPath, string> { [present] = "value" }, batch);
    }

    [Fact]
    public async Task A_deleted_secret_is_gone()
    {
        var provider = Provider(1, (1, Key1));
        var path     = UniquePath();
        var ct       = TestContext.Current.CancellationToken;
        await provider.SetSecretAsync(path, "value", ct);

        await provider.DeleteSecretAsync(path, ct);

        await Assert.ThrowsAsync<SecretNotFoundException>(() => provider.GetSecretAsync(path, ct));
    }

    [Fact]
    public async Task A_purge_deletes_every_secret_of_its_compartment_and_nothing_else()
    {
        var provider = Provider(1, (1, Key1));
        var ct       = TestContext.Current.CancellationToken;
        // Compartments no other test writes: the tests share one table.
        var target   = Compartment.Parse("p" + Guid.NewGuid().ToString("N"));
        var sibling  = Compartment.Parse(target.Value + "-2");
        var marker   = Guid.NewGuid().ToString("N");

        var purged = new[]
        {
            SecretPath.For<BillingModule>(target, marker, "a"),
            SecretPath.For<DesktopModule>(target, marker, "b"),
        };
        var kept = new[]
        {
            SecretPath.For<BillingModule>(sibling, marker),
            SecretPath.For<BillingModule>(Space.Shared, marker),
            SecretPath.ForAssembly<BillingModule>(marker),
        };
        foreach (var path in purged.Concat(kept))
            await provider.SetSecretAsync(path, "value", ct);

        Assert.Equal(2, await provider.PurgeCompartmentAsync(target, Actor.Parse("job:purge"), ct));

        Assert.Empty(await provider.GetSecretsAsync(purged, ct));
        Assert.Equal(kept.Length, (await provider.GetSecretsAsync(kept, ct)).Count);
        Assert.Equal(0, await provider.PurgeCompartmentAsync(target, Actor.Parse("job:purge"), ct));

        foreach (var path in kept)
            await provider.DeleteSecretAsync(path, ct);
    }

    [Fact]
    public void No_passphrase_for_the_active_slot_fails_at_construction()
    {
        var error = Assert.Throws<OptionsValidationException>(() => Provider(2, (1, Key1)));

        Assert.Contains("Keys:2", error.Message);
    }
}
