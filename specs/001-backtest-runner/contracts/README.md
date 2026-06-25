# Contracts: Backtest Runner

The backtest runner is an internal .NET library plus a CLI host — no network API. These contracts
define the seams between the independent layers (data, strategy, risk, execution), the **code
strategy-type** contract and its **stored configuration**, the **Strategy store** persistence, and
the **report/result schema**. They are what the tests target.

| Contract | File | Layer / concern |
|----------|------|-----------------|
| Strategy type + parameter spec | [strategy-type.md](./strategy-type.md) | Strategy (code) |
| Strategy factory | [strategy-factory.md](./strategy-factory.md) | Validate params + instantiate type |
| Strategy store | [strategy-store.md](./strategy-store.md) | Configuration / version / result persistence |
| Risk policy (central, authoritative) | [risk.md](./risk.md) | Risk (in `HTB.Shared/Risk`) |
| Execution / fill (+ SL/TP) | [execution.md](./execution.md) | Execution |
| Data source | [data-source.md](./data-source.md) | Data |
| Report + result schema | [report.md](./report.md) | Output (CLI) |

Signatures are illustrative shapes for planning — finalized during implementation. Money/quantity
types are `decimal`; times are `DateTimeOffset` (UTC); stored ids are `Guid`; I/O methods are
`async` with a `CancellationToken`. **Strategy types are code; only configuration is data.** The
strategy types, factory, and configuration store live in `HTB.Shared` so the identical
configuration drives both the backtest runner and future live trading ("tested = traded").
