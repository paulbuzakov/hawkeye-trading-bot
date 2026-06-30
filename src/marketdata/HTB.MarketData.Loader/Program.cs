using System.Diagnostics.CodeAnalysis;
using HTB.MarketData.Loader.Binance;
using HTB.MarketData.Loader.Configuration;
using HTB.MarketData.Loader.Ingestion;
using HTB.MarketData.Loader.Persistence;
using HTB.MarketData.Shared.Persistence;
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
    private const string ConnectionStringEnvVar = "HTB_CONNECTION_STRING";
    private const string VerifyEnvVar = "HTB_VERIFY";
    private const string BinanceBaseAddress = "https://api.binance.com";

    private static async Task<int> Main(string[] args)
    {
        var symbolsFile =
            args.Length > 0
                ? args[0]
                : Environment.GetEnvironmentVariable(SymbolsFileEnvVar)
                    ?? throw new InvalidOperationException(
                        $"Either pass the symbols file path as the first argument, or set the {SymbolsFileEnvVar} environment variable."
                    );

        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvVar)
            ?? throw new InvalidOperationException(
                $"Environment variable {ConnectionStringEnvVar} must be set to a valid PostgreSQL connection string."
            );

        // Opt-in reconciliation pass: re-scan every candle from its configured start and upsert,
        // correcting drifted bars and filling gaps. Anything other than "true"/"1" leaves the
        // loader in its default resume-only mode so a stray value never triggers a full re-scan.
        var verify = IsTruthy(Environment.GetEnvironmentVariable(VerifyEnvVar));

        try
        {
            var specs = await SymbolConfigParser.ParseFileAsync(symbolsFile);

            using var httpClient = new HttpClient { BaseAddress = new Uri(BinanceBaseAddress) };
            var client = new BinanceMarketDataClient(httpClient);

            var readDbOptions = new DbContextOptionsBuilder<MarketDataReadonlyDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            await using var readDb = new MarketDataReadonlyDbContext(readDbOptions);

            var writeDbOptions = new DbContextOptionsBuilder<MarketDataWriteDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            await using var writeDb = new MarketDataWriteDbContext(writeDbOptions);

            var loader = new MarketDataLoader(
                client,
                new InstrumentRepository(writeDb),
                new CandleRepository(readDb),
                new CandleWriter(writeDb),
                TimeProvider.System,
                Console.WriteLine
            );

            var written = await loader.LoadAsync(specs, verify);
            var mode = verify ? "verified" : "written";
            Console.WriteLine($"Done. {written} candles {mode} from {symbolsFile}.");
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Load failed: {ex.Message}");
            return 1;
        }
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase) || value?.Trim() == "1";
}
