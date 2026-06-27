using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HTB.MarketData.Shared.Domain;

public sealed class ExchangeCodeConverter()
    : ValueConverter<ExchangeCode, string>(id => id.Value, value => new ExchangeCode(value));
