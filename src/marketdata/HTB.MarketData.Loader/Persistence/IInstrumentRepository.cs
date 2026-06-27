using HTB.MarketData.Shared.Domain;

namespace HTB.MarketData.Loader.Persistence;

public interface IInstrumentRepository
{
    Task<Exchange> GetOrCreateExchangeAsync(
        ExchangeCode code,
        string name,
        CancellationToken cancellationToken = default
    );

    Task<Symbol> GetOrCreateSymbolAsync(
        ExchangeCode exchangeCode,
        SymbolCode symbolCode,
        string baseAsset,
        string quoteAsset,
        CancellationToken cancellationToken = default
    );
}
