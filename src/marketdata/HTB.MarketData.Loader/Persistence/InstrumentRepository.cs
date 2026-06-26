using HTB.Shared.MarketData.Domain;
using Microsoft.EntityFrameworkCore;

namespace HTB.MarketData.Loader.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IInstrumentRepository"/>. Each get-or-create looks the
/// row up by its natural key and inserts it only when missing, so concurrent backfills and
/// re-runs converge on the same exchange/symbol identifiers.
/// </summary>
public sealed class InstrumentRepository(MarketDataWriteDbContext db) : IInstrumentRepository
{
    private readonly MarketDataWriteDbContext _db = db;

    public async Task<Exchange> GetOrCreateExchangeAsync(
        string code,
        string name,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var existing = await _db.Exchanges.FirstOrDefaultAsync(
            e => e.Code == code,
            cancellationToken
        );
        if (existing is not null)
        {
            return existing;
        }

        var exchange = new Exchange { Code = code, Name = name };
        _db.Exchanges.Add(exchange);
        await _db.SaveChangesAsync(cancellationToken);
        return exchange;
    }

    public async Task<Symbol> GetOrCreateSymbolAsync(
        int exchangeId,
        string baseAsset,
        string quoteAsset,
        string exchangeSymbol,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeSymbol);

        var existing = await _db.Symbols.FirstOrDefaultAsync(
            s => s.ExchangeId == exchangeId && s.ExchangeSymbol == exchangeSymbol,
            cancellationToken
        );
        if (existing is not null)
        {
            return existing;
        }

        var symbol = new Symbol
        {
            ExchangeId = exchangeId,
            BaseAsset = baseAsset,
            QuoteAsset = quoteAsset,
            ExchangeSymbol = exchangeSymbol,
        };
        _db.Symbols.Add(symbol);
        await _db.SaveChangesAsync(cancellationToken);
        return symbol;
    }
}
