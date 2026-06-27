using HTB.MarketData.Shared.Domain;

namespace HTB.MarketData.Shared.Persistence;

/// <summary>
/// Shared EF Core model for the candle store (PostgreSQL + TimescaleDB). Maps the
/// exchange-agnostic domain model to the snake_case relational schema described in
/// docs/architecture/market-data-storage.md. Concrete subclasses pick a tracking posture:
/// <see cref="MarketDataReadonlyDbContext"/> for the no-tracking read side (and EF migration target),
/// and the loader's <c>MarketDataWriteDbContext</c> for the tracked write side.
/// </summary>
public abstract class MarketDataDbContextBase(DbContextOptions options) : DbContext(options)
{
    public DbSet<Exchange> Exchanges => Set<Exchange>();

    public DbSet<Symbol> Symbols => Set<Symbol>();

    public DbSet<Candle> Candles => Set<Candle>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<ExchangeCode>().HaveConversion<ExchangeCodeConverter>();
        configurationBuilder.Properties<SymbolCode>().HaveConversion<SymbolCodeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("marketdata");

        modelBuilder.Entity<Exchange>(entity =>
        {
            entity.ToTable("exchanges", "marketdata");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<Symbol>(entity =>
        {
            entity.ToTable("symbols", "marketdata");
            entity.HasKey(s => s.Code);
            entity.Property(s => s.Code).HasColumnName("code");
            entity.Property(s => s.Exchange).HasColumnName("exchange_code");
            entity.Property(s => s.BaseAsset).HasColumnName("base_asset").IsRequired();
            entity.Property(s => s.QuoteAsset).HasColumnName("quote_asset").IsRequired();
            entity.HasIndex(s => new { s.Exchange, s.Code }).IsUnique();
            entity.HasOne<Exchange>().WithMany().HasForeignKey(s => s.Exchange).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Candle>(entity =>
        {
            entity.ToTable("candles", "marketdata");
            entity.HasKey(c => new
            {
                c.Exchange,
                c.Symbol,
                c.Interval,
                c.OpenTime,
            });
            entity.Property(c => c.Exchange).HasColumnName("exchange_code");
            entity.Property(c => c.Symbol).HasColumnName("symbol_code");
            entity.Property(c => c.Interval).HasColumnName("interval").HasConversion<short>();
            entity.Property(c => c.OpenTime).HasColumnName("open_time");
            entity.Property(c => c.Open).HasColumnName("open");
            entity.Property(c => c.High).HasColumnName("high");
            entity.Property(c => c.Low).HasColumnName("low");
            entity.Property(c => c.Close).HasColumnName("close");
            entity.Property(c => c.Volume).HasColumnName("volume");
            entity.Property(c => c.QuoteVolume).HasColumnName("quote_volume");
            entity.Property(c => c.TradeCount).HasColumnName("trade_count");
            entity.Property(c => c.IsClosed).HasColumnName("is_closed");
            entity.Property(c => c.IngestedAt).HasColumnName("ingested_at").HasDefaultValueSql("now()");
            entity
                .HasIndex(c => new
                {
                    c.Symbol,
                    c.Interval,
                    c.OpenTime,
                })
                .HasDatabaseName("ix_candles_symbol_interval_time");
            entity.HasOne<Symbol>().WithMany().HasForeignKey(c => c.Symbol).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Exchange>().WithMany().HasForeignKey(c => c.Exchange).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
