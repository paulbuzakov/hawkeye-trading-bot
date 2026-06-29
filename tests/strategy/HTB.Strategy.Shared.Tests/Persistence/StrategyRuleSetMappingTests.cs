using HTB.Strategy.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HTB.Strategy.Shared.Tests.Persistence;

public sealed class StrategyRuleSetMappingTests
{
    private static readonly StoreObjectIdentifier Table =
        StoreObjectIdentifier.Table("strategy_rule_sets", "strategy");

    private static StrategyReadonlyDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<StrategyReadonlyDbContext>()
            .UseNpgsql("Host=localhost;Database=hawkeye;Username=hawkeye;Password=hawkeye")
            .Options;
        return new StrategyReadonlyDbContext(options);
    }

    [Fact]
    public void Maps_rule_sets_to_the_strategy_rule_sets_table()
    {
        using var context = BuildContext();

        var entity = context.Model.FindEntityType(typeof(StrategyRuleSetRow));

        Assert.NotNull(entity);
        Assert.Equal("strategy_rule_sets", entity!.GetTableName());
        Assert.Equal("strategy", entity.GetSchema());
    }

    [Fact]
    public void Keys_rule_sets_on_the_version_id_column()
    {
        using var context = BuildContext();
        var entity = context.Model.FindEntityType(typeof(StrategyRuleSetRow))!;

        var key = entity.FindPrimaryKey()!;
        var property = Assert.Single(key.Properties);

        Assert.Equal(nameof(StrategyRuleSetRow.VersionId), property.Name);
        Assert.Equal("version_id", property.GetColumnName(Table));
    }

    [Fact]
    public void Stores_the_rule_body_as_a_required_jsonb_column()
    {
        using var context = BuildContext();
        var entity = context.Model.FindEntityType(typeof(StrategyRuleSetRow))!;

        var rules = entity.FindProperty(nameof(StrategyRuleSetRow.Rules))!;

        Assert.Equal("rules", rules.GetColumnName(Table));
        Assert.Equal("jsonb", rules.GetColumnType());
        Assert.False(rules.IsNullable);
    }
}
