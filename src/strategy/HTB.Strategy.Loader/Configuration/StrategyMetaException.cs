namespace HTB.Strategy.Loader.Configuration;

/// <summary>
/// Thrown when the loader's inputs are invalid: a missing or malformed <c>--meta</c> argument,
/// or a <c>meta.json</c> that is not valid JSON or fails validation (missing id/version/name,
/// empty scope, unknown timeframe, negative warmup).
/// </summary>
public sealed class StrategyMetaException(string message) : Exception(message);
