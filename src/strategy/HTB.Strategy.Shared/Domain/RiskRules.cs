namespace HTB.Strategy.Shared.Domain;

/// <summary>
/// The <c>risk</c> block of a bundle's <c>rules.json</c> — position sizing, optional protective
/// brackets, and concurrency caps. <see cref="StopLoss"/> and <see cref="TakeProfit"/> are optional;
/// the open-position caps are strictly positive (a cap of zero would forbid trading entirely).
/// </summary>
public sealed record RiskRules
{
    public RiskRules(
        PositionSizing positionSizing,
        ProtectiveExit? stopLoss,
        ProtectiveExit? takeProfit,
        int maxOpenPositions,
        int maxOpenPerSymbol
    )
    {
        ArgumentNullException.ThrowIfNull(positionSizing);

        if (maxOpenPositions < 1)
        {
            throw new StrategyDomainException(
                $"Risk maxOpenPositions must be at least 1 (was {maxOpenPositions})."
            );
        }

        if (maxOpenPerSymbol < 1)
        {
            throw new StrategyDomainException(
                $"Risk maxOpenPerSymbol must be at least 1 (was {maxOpenPerSymbol})."
            );
        }

        PositionSizing = positionSizing;
        StopLoss = stopLoss;
        TakeProfit = takeProfit;
        MaxOpenPositions = maxOpenPositions;
        MaxOpenPerSymbol = maxOpenPerSymbol;
    }

    /// <summary>How each entry is sized.</summary>
    public PositionSizing PositionSizing { get; }

    /// <summary>Stop-loss bracket, or <c>null</c> if the strategy declares none.</summary>
    public ProtectiveExit? StopLoss { get; }

    /// <summary>Take-profit bracket, or <c>null</c> if the strategy declares none.</summary>
    public ProtectiveExit? TakeProfit { get; }

    /// <summary>Maximum concurrent open positions across all symbols.</summary>
    public int MaxOpenPositions { get; }

    /// <summary>Maximum concurrent open positions for any single symbol.</summary>
    public int MaxOpenPerSymbol { get; }
}
