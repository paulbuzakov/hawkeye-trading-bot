using HTB.MarketData.Shared.Domain;
using HTB.Strategy.Shared.Domain;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HTB.Strategy.Shared.Persistence;

/// <summary>
/// Shared EF Core model for the strategy store (PostgreSQL + TimescaleDB). Maps the versioned
/// <see cref="StrategyDefinition"/> (the shape of a bundle's <c>meta.json</c>) to the snake_case
/// relational schema. Concrete subclasses pick a tracking posture:
/// <see cref="StrategyReadonlyDbContext"/> for the no-tracking read side (and EF migration target),
/// shared with any future tracked write-side context.
/// </summary>
public abstract class StrategyDbContextBase(DbContextOptions options) : DbContext(options)
{
    internal const string Schema = "strategy";

    public DbSet<StrategyDefinition> StrategyDefinitions => Set<StrategyDefinition>();

    public DbSet<StrategyRuleSetRow> StrategyRuleSets => Set<StrategyRuleSetRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<StrategyId>().HaveConversion<StrategyIdConverter>();
        configurationBuilder.Properties<StrategyVersion>().HaveConversion<StrategyVersionConverter>();
        configurationBuilder.Properties<StrategyVersionId>().HaveConversion<StrategyVersionIdConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // The rule set is a deep, union-typed aggregate, so it is stored as a single jsonb document
        // rather than shredded into relational tables. Its primary key (id, version) is also its
        // foreign key to the owning definition — the EF Core 1:1 shared-primary-key pattern — so the
        // database enforces "no rule set without a definition" and cascades on delete.
        modelBuilder.Entity<StrategyRuleSetRow>(entity =>
        {
            entity.ToTable("strategy_rule_sets", Schema);
            entity.HasKey(r => new { r.Id, r.Version });
            entity.Property(r => r.Id).HasColumnName("id");
            entity.Property(r => r.Version).HasColumnName("version");
            entity.Ignore(r => r.VersionId);
            entity.Property(r => r.Rules).HasColumnName("rules").HasColumnType("jsonb").IsRequired();

            entity
                .HasOne(r => r.Definition)
                .WithOne(d => d.RuleSet)
                .HasForeignKey<StrategyRuleSetRow>(r => new { r.Id, r.Version })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StrategyDefinition>(entity =>
        {
            entity.ToTable("strategy_definitions", Schema);

            // Identity is the version id: a strategy family (id) at a specific, immutable version.
            entity.HasKey(d => new { d.Id, d.Version });
            entity.Property(d => d.Id).HasColumnName("id");
            entity.Property(d => d.Version).HasColumnName("version");
            entity.Ignore(d => d.VersionId);

            entity.Property(d => d.Name).HasColumnName("name").IsRequired();
            entity.Property(d => d.Description).HasColumnName("description").IsRequired();

            // Descriptive metadata and declared market scope, stored as PostgreSQL arrays.
            entity
                .Property(d => d.Tags)
                .HasColumnName("tags")
                .HasConversion(
                    new ValueConverter<IReadOnlyList<string>, string[]>(v => v.ToArray(), v => v.ToList()),
                    ListComparer<string>()
                );
            entity
                .Property(d => d.Exchanges)
                .HasColumnName("exchanges")
                .HasConversion(
                    new ValueConverter<IReadOnlyList<ExchangeCode>, string[]>(
                        v => v.Select(c => c.Value).ToArray(),
                        v => v.Select(s => new ExchangeCode(s)).ToList()
                    ),
                    ListComparer<ExchangeCode>()
                );
            entity
                .Property(d => d.Symbols)
                .HasColumnName("symbols")
                .HasConversion(
                    new ValueConverter<IReadOnlyList<SymbolCode>, string[]>(
                        v => v.Select(c => c.Value).ToArray(),
                        v => v.Select(s => new SymbolCode(s)).ToList()
                    ),
                    ListComparer<SymbolCode>()
                );
            entity
                .Property(d => d.Timeframes)
                .HasColumnName("timeframes")
                .HasConversion(
                    new ValueConverter<IReadOnlyList<Timeframe>, short[]>(
                        v => v.Select(t => (short)t).ToArray(),
                        v => v.Select(s => (Timeframe)s).ToList()
                    ),
                    ListComparer<Timeframe>()
                );

            entity.Property(d => d.WarmupBars).HasColumnName("warmup_bars");
        });

        static ValueComparer<IReadOnlyList<T>> ListComparer<T>() =>
            new(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item!.GetHashCode())),
                v => v.ToList()
            );
    }
}
