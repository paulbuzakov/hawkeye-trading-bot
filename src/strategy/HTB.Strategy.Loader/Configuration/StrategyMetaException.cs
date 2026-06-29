namespace HTB.Strategy.Loader.Configuration;

/// <summary>
/// Thrown when the loader's inputs are invalid: a missing/malformed <c>--meta</c> or
/// <c>--rules</c> argument, a <c>meta.json</c> that is not valid JSON or fails validation
/// (missing id/version/name, empty scope, unknown timeframe, negative warmup), or a
/// <c>rules.json</c> that is not valid JSON or fails format/mapping validation (missing block,
/// unknown direction/type/operator/source, or an unrecognized operand token).
/// </summary>
public sealed class StrategyMetaException(string message) : Exception(message);
