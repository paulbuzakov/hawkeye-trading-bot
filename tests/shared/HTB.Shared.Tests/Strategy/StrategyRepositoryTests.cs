using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Persistence;
using HTB.Strategy.Loader.Persistence;

namespace HTB.Shared.Tests.Strategy;

[Collection(nameof(StrategyDatabaseCollection))]
public sealed class StrategyRepositoryTests(StrategyDatabaseFixture fixture)
{
    private readonly StrategyDatabaseFixture _fixture = fixture;

    // The read repository never writes; arrange rows through the loader's write path.
    private async Task SeedAsync(StrategyPersistenceData.Loaded loaded)
    {
        await using var db = _fixture.CreateWriteContext();
        await new StrategyStore(db, TimeProvider.System).SaveAsync(
            loaded.Definition,
            loaded.MetaJson,
            loaded.RulesJson
        );
    }

    private StrategyRepository NewRepository() => new(_fixture.CreateContext());

    [Fact]
    public async Task GetAsync_returns_the_version_and_null_when_absent()
    {
        await SeedAsync(StrategyPersistenceData.Active("get", 1));
        var repository = NewRepository();

        var found = await repository.GetAsync("get", 1);
        var missing = await repository.GetAsync("get", 99);

        Assert.NotNull(found);
        Assert.Equal("get", found!.StrategyId);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetLatestAsync_returns_the_highest_version_number()
    {
        await SeedAsync(StrategyPersistenceData.Active("latest", 1));
        await SeedAsync(StrategyPersistenceData.Active("latest", 2));
        var repository = NewRepository();

        var latest = await repository.GetLatestAsync("latest");
        var none = await repository.GetLatestAsync("no-such-strategy");

        Assert.Equal(2, latest!.VersionNumber);
        Assert.Null(none);
    }

    [Fact]
    public async Task ListAsync_filters_by_status_when_requested()
    {
        await SeedAsync(StrategyPersistenceData.Active("list-active", 1));
        await SeedAsync(
            StrategyPersistenceData.Draft("list-draft", 1, "sha256:" + new string('c', 64))
        );
        var repository = NewRepository();

        var all = await repository.ListAsync();
        var actives = await repository.ListAsync(StrategyStatus.Active);

        Assert.Contains(all, s => s.StrategyId == "list-active");
        Assert.Contains(all, s => s.StrategyId == "list-draft");
        Assert.Contains(actives, s => s.StrategyId == "list-active");
        Assert.DoesNotContain(actives, s => s.StrategyId == "list-draft");
    }
}
