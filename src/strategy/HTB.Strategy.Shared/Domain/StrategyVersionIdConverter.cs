using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// EF Core value converter that stores a <see cref="StrategyVersionId"/> as its canonical
/// <c>{id}@{version}</c> string (e.g. <c>rsi-movement@1</c>), used as the rule-set table key.
/// </summary>
public sealed class StrategyVersionIdConverter()
    : ValueConverter<StrategyVersionId, string>(id => id.ToString(), value => StrategyVersionId.Parse(value));
