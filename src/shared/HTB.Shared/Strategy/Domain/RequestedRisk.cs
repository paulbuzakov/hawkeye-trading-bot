namespace HTB.Shared.Strategy.Domain;

/// <summary>
/// The risk envelope a strategy <em>asks</em> for. Advisory only: the risk layer has the final say
/// and may veto or resize (see the format doc §2.5). Percentages are whole-number percents
/// (e.g. <c>2.0</c> means 2%).
/// </summary>
public sealed record RequestedRisk(
    decimal StopLossPct,
    decimal TakeProfitPct,
    decimal MaxPositionPct
);
