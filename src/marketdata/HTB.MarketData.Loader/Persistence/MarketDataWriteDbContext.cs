using HTB.MarketData.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTB.MarketData.Loader.Persistence;

/// <summary>
/// Read/write context the loader ingests through. Tracks entities (so get-or-create can stage
/// inserts) and shares the relational mapping with the read side via
/// <see cref="MarketDataDbContextBase"/>. The migration target is the read-only
/// <see cref="MarketDataReadonlyDbContext"/> in HTB.Shared; this context maps the same tables and is
/// never migrated through.
/// </summary>
public sealed class MarketDataWriteDbContext(DbContextOptions<MarketDataWriteDbContext> options)
    : MarketDataDbContextBase(options);
