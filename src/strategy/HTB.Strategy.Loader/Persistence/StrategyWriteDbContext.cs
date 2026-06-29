using HTB.Strategy.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTB.Strategy.Loader.Persistence;

/// <summary>
/// Read/write context the loader persists through. Tracks entities so definitions can be staged
/// and updated, and shares the relational mapping with the read side via
/// <see cref="StrategyDbContextBase"/>. The migration target is the read-only
/// <c>StrategyReadonlyDbContext</c> in HTB.Strategy.Shared; this context maps the same tables and
/// is never migrated through.
/// </summary>
public sealed class StrategyWriteDbContext(DbContextOptions<StrategyWriteDbContext> options)
    : StrategyDbContextBase(options);
