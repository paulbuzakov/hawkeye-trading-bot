namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// The side(s) a strategy is allowed to trade — the <c>direction</c> field of <c>rules.json</c>.
/// Stored as a stable numeric code; never renumber existing members.
/// </summary>
public enum TradeDirection : byte
{
    /// <summary>Only long entries.</summary>
    LongOnly = 1,

    /// <summary>Only short entries.</summary>
    ShortOnly = 2,

    /// <summary>Both long and short entries.</summary>
    Both = 3,
}
