using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HTB.Strategy.Shared.Domain;

public sealed class StrategyIdConverter()
    : ValueConverter<StrategyId, string>(id => id.Value, value => new StrategyId(value));
