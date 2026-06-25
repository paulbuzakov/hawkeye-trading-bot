using System.Diagnostics.CodeAnalysis;
using HTB.MarketData.Loader.Binance;
using HTB.MarketData.Loader.Configuration;
using HTB.MarketData.Loader.Ingestion;
using HTB.Shared.MarketData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTB.MarketData.Loader;

/// <summary>
/// Console entry point: reads the <c>symbols.json</c> manifest and backfills Binance candles
/// into the market-data store. Pure composition (real HTTP + PostgreSQL wiring), so it is
/// excluded from coverage; the testable logic lives in the parser, client, and loader types.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class Program
{
    private const string SymbolsFileEnvVar = "HTB_SYMBOLS_FILE";
    private const string ConnectionStringEnvVar = "HTB_MARKETDATA_DB";
    private const string DefaultSymbolsFile = "symbols.json";
    private const string BinanceBaseAddress = "https://api.binance.com";

    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=hawkeye;Username=hawkeye;Password=hawkeye";

    private static async Task<int> Main(string[] args)
    {
        var symbolsFile =
            args.Length > 0
                ? args[0]
                : Environment.GetEnvironmentVariable(SymbolsFileEnvVar) ?? DefaultSymbolsFile;
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvVar) ?? DefaultConnectionString;

        try
        {
            var specs = await SymbolConfigParser.ParseFileAsync(symbolsFile);

            using var httpClient = new HttpClient { BaseAddress = new Uri(BinanceBaseAddress) };
            var client = new BinanceMarketDataClient(httpClient);

            var options = new DbContextOptionsBuilder<MarketDataDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var db = new MarketDataDbContext(options);

            var loader = new MarketDataLoader(
                client,
                new InstrumentRepository(db),
                new CandleRepository(db),
                TimeProvider.System,
                Console.WriteLine
            );

            var written = await loader.LoadAsync(specs);
            Console.WriteLine($"Done. {written} candles written from {symbolsFile}.");
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Load failed: {ex.Message}");
            return 1;
        }
    }
}
