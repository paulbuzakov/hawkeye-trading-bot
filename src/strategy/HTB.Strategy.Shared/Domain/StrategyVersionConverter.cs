using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HTB.Strategy.Shared.Domain;

public sealed class StrategyVersionConverter()
    : ValueConverter<StrategyVersion, int>(version => version.Value, value => new StrategyVersion(value));
