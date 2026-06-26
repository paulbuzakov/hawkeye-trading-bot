using HTB.Shared.MarketData.Domain;

namespace HTB.MarketData.Loader.Persistence;

/// <summary>
/// Resolves the <see cref="Exchange"/> and <see cref="Symbol"/> rows that candle foreign
/// keys point at, creating them on first use. Lookups are idempotent on the natural keys
/// (<see cref="Exchange.Code"/> and (<see cref="Symbol.ExchangeId"/>,
/// <see cref="Symbol.ExchangeSymbol"/>)) so repeated loads reuse the same identifiers. This is a
/// write concern, so it lives in the loader rather than read-only HTB.Shared.
/// </summary>
public interface IInstrumentRepository
{
    /// <summary>
    /// Returns the exchange with the given <paramref name="code"/>, inserting it with
    /// <paramref name="name"/> if it does not exist yet.
    /// </summary>
    Task<Exchange> GetOrCreateExchangeAsync(
        string code,
        string name,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Returns the symbol identified by (<paramref name="exchangeId"/>,
    /// <paramref name="exchangeSymbol"/>), inserting it with the given asset metadata if it
    /// does not exist yet.
    /// </summary>
    Task<Symbol> GetOrCreateSymbolAsync(
        int exchangeId,
        string baseAsset,
        string quoteAsset,
        string exchangeSymbol,
        CancellationToken cancellationToken = default
    );
}
