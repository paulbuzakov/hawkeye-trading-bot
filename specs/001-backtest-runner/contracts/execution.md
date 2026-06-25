# Contract: Execution / Fill Simulator (+ stop-loss / take-profit)

**Namespace**: `HTB.Backtest.Execution` · **Satisfies**: FR-004, FR-006, FR-003g · **Ref**: R1, R8

Turns a risk-approved order into a simulated fill (next-bar-open + fees + slippage), applies it to
the portfolio, and evaluates configured stop-loss / take-profit exits on open positions.

```csharp
public interface IFillSimulator
{
    // fillCandle is the candle AFTER the signal candle (R1). Returns the resulting fill.
    SimulatedOrder Fill(OrderIntent approved, Candle fillCandle, FeeModel fees, SlippageModel slippage);
}

public sealed class StopTakeProfitEvaluator
{
    // Returns an exit (with reason) if price reached the configured stop/target this candle.
    ExitDecision? Evaluate(OpenPosition position, Candle candle, decimal stopLossPct, decimal takeProfitPct);
}

public sealed class Portfolio
{
    PortfolioSnapshot Snapshot { get; }
    void Apply(SimulatedOrder order);   // updates cash, position, average entry; never < 0 cash
}
```

**Rules**
- Fill price = `fillCandle.Open` adjusted **against** the trader by `slippage` (buys higher, sells
  lower). Fee = `fees.Rate × tradedValue`; zero fees/slippage ⇒ ideal baseline.
- **Stop-loss / take-profit**: evaluated each candle on the open position; if both are reachable
  within one candle, **stop-loss wins** (deterministic precedence). Exit reason is recorded on the
  trade ({Signal, StopLoss, TakeProfit, EndOfRange}).
- All arithmetic in `decimal`; cash MUST NOT go negative.
- An order signalled on the last candle has no next bar ⇒ not filled; an open position at end of
  range is marked to the last candle's close and reported (EndOfRange).
