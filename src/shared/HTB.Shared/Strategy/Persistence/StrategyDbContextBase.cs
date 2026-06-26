namespace HTB.Shared.Strategy.Persistence;

/// <summary>
/// Shared EF Core model for the strategy registry (PostgreSQL). Maps the
/// <see cref="StrategyVersionRecord"/> document-plus-projection rows to the snake_case
/// <c>strategy</c> schema. Concrete subclasses pick a tracking posture:
/// <see cref="StrategyReadonlyDbContext"/> for the no-tracking read side (and EF migration target),
/// and the loader's <c>StrategyWriteDbContext</c> for the tracked write side. Mirrors
/// <c>MarketDataDbContextBase</c>.
/// </summary>
public abstract class StrategyDbContextBase(DbContextOptions options) : DbContext(options)
{
    public DbSet<StrategyVersionRecord> StrategyVersions => Set<StrategyVersionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("strategy");

        modelBuilder.Entity<StrategyVersionRecord>(entity =>
        {
            entity.ToTable("strategy_versions", "strategy");
            entity.HasKey(s => new { s.StrategyId, s.VersionNumber });
            entity.Property(s => s.StrategyId).HasColumnName("strategy_id");
            entity.Property(s => s.VersionNumber).HasColumnName("version_number");
            entity.Property(s => s.Name).HasColumnName("name").IsRequired();
            entity.Property(s => s.Status).HasColumnName("status").HasConversion<short>();
            entity.Property(s => s.SchemaVersion).HasColumnName("schema_version").IsRequired();
            entity.Property(s => s.RulesHash).HasColumnName("rules_hash").IsRequired();
            entity.Property(s => s.Timeframe).HasColumnName("timeframe").HasConversion<short>();
            entity.Property(s => s.WarmupBars).HasColumnName("warmup_bars");
            entity.Property(s => s.MetaJson).HasColumnName("meta_json").IsRequired();
            entity.Property(s => s.RulesJson).HasColumnName("rules_json").IsRequired();
            entity.Property(s => s.CreatedAt).HasColumnName("created_at");
            entity
                .Property(s => s.RegisteredAt)
                .HasColumnName("registered_at")
                .HasDefaultValueSql("now()");
            entity.HasIndex(s => s.Status).HasDatabaseName("ix_strategy_versions_status");
        });
    }
}
