using HTB.Shared.Strategy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTB.Strategy.Loader.Persistence;

/// <summary>
/// Read/write context the loader registers strategies through. Tracks entities (so the registry's
/// read-modify-decide save can stage inserts/updates) and shares the relational mapping with the
/// read side via <see cref="StrategyDbContextBase"/>. The migration target is the read-only
/// <see cref="StrategyReadonlyDbContext"/> in HTB.Shared; this context maps the same table and is
/// never migrated through. Mirrors <c>MarketDataWriteDbContext</c>.
/// </summary>
public sealed class StrategyWriteDbContext(DbContextOptions<StrategyWriteDbContext> options)
    : StrategyDbContextBase(options);
