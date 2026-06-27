using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HTB.MarketData.Shared.Domain;

public sealed class SymbolCodeConverter()
    : ValueConverter<SymbolCode, string>(id => id.Value, value => new SymbolCode(value));
