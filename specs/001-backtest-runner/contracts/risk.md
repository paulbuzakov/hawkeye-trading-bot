# Contract: Risk Policy (central, authoritative)

**Namespace**: `HTB.Shared.Risk` · **Satisfies**: FR-005, FR-003g, FR-008, US2, constitution II · **Ref**: R8

The single authoritative gate every simulated order passes through — the same component a future
live executor uses, so backtest and live risk behavior cannot diverge. The configuration's risk
block is **advisory input**; this layer has final say and may veto or resize.

```csharp
public interface IRiskPolicy
{
    RiskDecision Evaluate(OrderIntent intent, PortfolioSnapshot portfolio, RiskState state);
}
```

Implementations:
- **PassThroughRiskPolicy** — approves all (used by US1 so the order path exists before enforcement).
- **CompositeRiskPolicy** — authoritative checks (US2):
  1. **Max position size** — resulting position > limit ⇒ `Rejected` or `Resized` per `OnBreach`.
  2. **Max daily loss** — current UTC day's realized loss ≥ limit ⇒ `Halted` for the rest of the day.
  3. **Kill-switch** — equity drawdown ≥ limit (or once tripped) ⇒ `Halted` for the rest of the run.

**Rules**
- Fail closed: any ambiguity resolves to the more restrictive outcome (constitution III).
- The effective limits derive from the configuration's *requested* risk plus global defaults; the
  central layer's decision is final regardless of what the configuration requested (SC-003).
- Every non-`Approved` decision yields a `RiskEvent` recorded in the report (FR-008).
- Configured **stop-loss / take-profit** exits are evaluated in the execution layer (see
  [execution.md](./execution.md)), not here; this contract governs order admission/sizing.
