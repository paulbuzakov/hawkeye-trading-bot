namespace HTB.Shared.Strategy.Strategy;

/// <summary>
/// Thrown when <c>meta.json</c>/<c>rules.json</c> are malformed or fail validation: bad schema
/// version, hash mismatch, undeclared or out-of-range parameter, unknown indicator kind, negative
/// offset, identity mismatch, malformed condition node, and the like. Mirrors the market-data
/// loader's typed <c>SymbolConfigException</c>.
/// </summary>
public sealed class StrategyConfigException(string message) : Exception(message);
