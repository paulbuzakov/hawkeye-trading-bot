using HTB.MarketData.Shared.Domain;

namespace HTB.MarketData.Loader.Configuration;

/// <summary>
/// One entry from <c>symbols.json</c>: which ticker to backfill, at which
/// <see cref="Timeframe"/>s, over which (inclusive) date range. <see cref="To"/> is optional —
/// a missing upper bound means "up to now".
/// </summary>
public sealed record SymbolLoadSpec(
    string Ticker,
    IReadOnlyList<Timeframe> Timeframes,
    DateTimeOffset From,
    DateTimeOffset? To
);
