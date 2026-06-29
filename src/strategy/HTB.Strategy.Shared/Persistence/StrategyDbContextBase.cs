namespace HTB.Strategy.Shared.Persistence;

/// <summary>
/// Shared EF Core model for the strategy store (PostgreSQL + TimescaleDB). Concrete
/// subclasses pick a tracking posture: <see cref="StrategyReadonlyDbContext"/> for the
/// no-tracking read side (and EF migration target). The model is currently empty — this is
/// the scaffold that future strategy entities (signals, runs, parameters) will hang off of.
/// </summary>
public abstract class StrategyDbContextBase(DbContextOptions options) : DbContext(options)
{
    internal const string Schema = "strategy";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
    }
}
