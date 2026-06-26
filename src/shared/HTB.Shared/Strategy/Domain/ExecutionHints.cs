namespace HTB.Shared.Strategy.Domain;

/// <summary>How the order router is <em>hinted</em> to execute a filled intent. Advisory only.</summary>
public sealed record ExecutionHints(
    OrderType OrderType,
    TimeInForce TimeInForce,
    decimal SlippageTolerancePct
);

/// <summary>The order type a strategy requests.</summary>
public enum OrderType : short
{
    Market = 1,
    Limit = 2,
}

/// <summary>The time-in-force a strategy requests.</summary>
public enum TimeInForce : short
{
    /// <summary>Good-til-cancelled.</summary>
    Gtc = 1,

    /// <summary>Immediate-or-cancel.</summary>
    Ioc = 2,

    /// <summary>Fill-or-kill.</summary>
    Fok = 3,
}
