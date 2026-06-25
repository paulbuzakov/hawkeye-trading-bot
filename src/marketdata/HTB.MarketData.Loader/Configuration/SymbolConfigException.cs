namespace HTB.MarketData.Loader.Configuration;

/// <summary>
/// Thrown when <c>symbols.json</c> is malformed or fails validation (missing ticker, empty
/// timeframes, unknown timeframe code, bad date, or an inverted date range).
/// </summary>
public sealed class SymbolConfigException(string message) : Exception(message);
