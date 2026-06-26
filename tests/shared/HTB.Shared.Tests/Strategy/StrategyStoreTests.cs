using HTB.Shared.MarketData.Domain;
using HTB.Shared.Strategy.Domain;
using HTB.Strategy.Loader.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTB.Shared.Tests.Strategy;

[Collection(nameof(StrategyDatabaseCollection))]
public sealed class StrategyStoreTests(StrategyDatabaseFixture fixture)
{
    private static readonly string HashA = "sha256:" + new string('a', 64);
    private static readonly string HashB = "sha256:" + new string('b', 64);

    private readonly StrategyDatabaseFixture _fixture = fixture;

    // ---- guards (no database access) -------------------------------------

    [Fact]
    public async Task SaveAsync_null_arguments_throw()
    {
        await using var db = _fixture.CreateWriteContext();
        var store = new StrategyStore(db, TimeProvider.System);
        var loaded = StrategyPersistenceData.Active("guard", 1);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.SaveAsync(null!, loaded.MetaJson, loaded.RulesJson)
        );
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.SaveAsync(loaded.Definition, null!, loaded.RulesJson)
        );
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.SaveAsync(loaded.Definition, loaded.MetaJson, null!)
        );
    }

    // ---- save logic ------------------------------------------------------

    [Fact]
    public async Task SaveAsync_inserts_a_new_version()
    {
        var loaded = StrategyPersistenceData.Active("insert", 1);

        var saved = await SaveAsync(loaded);

        Assert.Equal("insert", saved.StrategyId);
        Assert.Equal(1, saved.VersionNumber);

        var stored = await GetAsync("insert", 1);
        Assert.NotNull(stored);
        Assert.Equal(StrategyStatus.Active, stored!.Status);
        Assert.Equal(loaded.RulesJson, stored.RulesJson);
        Assert.Equal(loaded.MetaJson, stored.MetaJson);
        Assert.Equal(Timeframe.H1, stored.Timeframe);
    }

    [Fact]
    public async Task SaveAsync_is_idempotent_for_identical_bytes()
    {
        var loaded = StrategyPersistenceData.Active("idem", 1);

        await SaveAsync(loaded);
        await SaveAsync(loaded);

        await using var db = _fixture.CreateContext();
        var count = await db.StrategyVersions.CountAsync(s => s.StrategyId == "idem");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SaveAsync_overwrites_a_draft_with_new_bytes()
    {
        await SaveAsync(StrategyPersistenceData.Draft("draftov", 1, HashA));

        var updated = await SaveAsync(StrategyPersistenceData.Draft("draftov", 1, HashB));

        Assert.Equal(HashB, updated.RulesHash);
        var stored = await GetAsync("draftov", 1);
        Assert.Equal(HashB, stored!.RulesHash);
    }

    [Fact]
    public async Task SaveAsync_rejects_redefining_an_immutable_active_version()
    {
        await SaveAsync(StrategyPersistenceData.Active("immutable", 1, threshold: 0));

        var conflicting = StrategyPersistenceData.Active("immutable", 1, threshold: 1);

        var ex = await Assert.ThrowsAsync<StrategyRegistryConflictException>(
            () => SaveAsync(conflicting)
        );
        Assert.Contains("immutable", ex.Message);
    }

    // ---- helpers ---------------------------------------------------------

    private async Task<Shared.Strategy.Persistence.StrategyVersionRecord> SaveAsync(
        StrategyPersistenceData.Loaded loaded
    )
    {
        await using var db = _fixture.CreateWriteContext();
        var store = new StrategyStore(db, TimeProvider.System);
        return await store.SaveAsync(loaded.Definition, loaded.MetaJson, loaded.RulesJson);
    }

    private async Task<Shared.Strategy.Persistence.StrategyVersionRecord?> GetAsync(
        string id,
        int version
    )
    {
        await using var db = _fixture.CreateContext();
        return await db.StrategyVersions.FirstOrDefaultAsync(
            s => s.StrategyId == id && s.VersionNumber == version
        );
    }
}
