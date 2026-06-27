namespace HTB.MarketData.Shared.Domain;

public sealed class MarketDataDomainException(string message) : Exception(message);
