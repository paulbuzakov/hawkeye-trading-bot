<!--
SYNC IMPACT REPORT
==================
Version change: (none) → 1.0.0  (initial ratification)
Bump rationale: First adoption of the project constitution. No prior version existed;
                all template placeholders replaced with concrete governance.

Modified principles: N/A (initial creation)

Added sections:
  - Core Principles (7 principles):
      I.   Security First
      II.  Capital Safety (NON-NEGOTIABLE)
      III. Reliability & Fail-Closed
      IV.  Test-First Discipline (NON-NEGOTIABLE)
      V.   Layered Architecture & Dependency Injection
      VI.  Observability & Auditability
      VII. Deterministic Financial Arithmetic
  - Technology & Security Constraints
  - Development Workflow & Quality Gates
  - Governance

Removed sections: None

Templates requiring updates:
  ✅ .specify/templates/plan-template.md   — Constitution Check gate references generic
                                             "constitution file"; compatible, no edit required.
  ✅ .specify/templates/spec-template.md   — No principle-specific sections needed; compatible.
  ✅ .specify/templates/tasks-template.md  — Test/observability/security task categories already
                                             present as samples; compatible.
  ⚠ Runtime guidance docs (README.md / docs/quickstart.md) — none present yet; create when added
     and reference these principles.

Follow-up TODOs: None. Ratification date set to initial adoption date.
-->

# Hawkeye Trading Bot Constitution

A production cryptocurrency trading bot built in C# on .NET 10. Real capital is at risk on
every execution path; these principles are the non-negotiable foundation that keeps funds,
secrets, and correctness safe. They bind all code, reviews, and design decisions.

## Core Principles

### I. Security First

Credentials are never trusted to source, logs, or history.

- API keys, secrets, and credentials MUST be loaded exclusively from environment variables or a
  secrets manager. Hardcoding any secret in source, config committed to the repo, or test
  fixtures is prohibited.
- Secrets MUST NEVER be written to logs, metrics, exception messages, or telemetry. Full order
  payloads and any field carrying sensitive data MUST be redacted before logging.
- Configuration that would expose a secret if logged MUST be wrapped so accidental serialization
  cannot leak it.

**Rationale**: A single leaked key grants an attacker direct access to funds. Prevention at the
boundary is the only reliable control; redaction-by-default removes the human error path.

### II. Capital Safety (NON-NEGOTIABLE)

No trade reaches an exchange without passing risk control, and the system defaults to not
trading real money.

- Every order path MUST enforce risk limits before submission: max position size, max daily
  loss, and a kill-switch that halts all trading when tripped.
- An order that fails any risk check MUST be rejected. There is no override path that bypasses
  risk checks in live mode.
- The system MUST default to dry-run / paper mode. Live trading MUST require explicit, deliberate
  configuration to enable; absence or ambiguity of that setting means paper mode.

**Rationale**: The worst outcomes (runaway losses, fat-finger orders, strategy bugs draining an
account) are all preventable at the risk gate. Defaulting to paper mode makes "trade real money"
an intentional act, never an accident.

### III. Reliability & Fail-Closed

External calls are assumed to fail, and failure never results in unintended trading.

- All exchange and external API calls MUST use explicit timeouts, retries with backoff, and
  idempotency keys to make retries safe against duplicate orders.
- On error, ambiguity, or timeout, the system MUST fail closed — decline to trade, do not retry
  blindly into a possible duplicate, and surface the failure. Failing open (proceeding as if
  success) is prohibited.
- Order submission MUST be idempotent: a retried request MUST NOT create a second position.

**Rationale**: Network and exchange failures are routine. Idempotency plus fail-closed ensures a
flaky connection causes at worst a missed trade, never a duplicated or phantom one.

### IV. Test-First Discipline (NON-NEGOTIABLE)

Trading, risk, and execution logic is proven by tests before it ships.

- TDD is required for all trading, risk, and order-execution logic: write the failing test,
  confirm it fails, then implement.
- Unit tests MUST cover edge cases (boundary sizes, zero/negative inputs, partial fills,
  rejections, timeouts, kill-switch activation).
- Tests MUST use mocked exchange clients. Live or network calls to real exchanges in tests are
  prohibited.

**Rationale**: Financial logic has no safe "fix in production" path. Tests written first define
the contract, edge-case coverage catches the conditions that lose money, and mocked clients keep
the suite deterministic and safe to run anywhere.

### V. Layered Architecture & Dependency Injection

Strategy, execution, risk, and data are independent, substitutable layers.

- The strategy, execution, risk, and data layers MUST be separated with clear boundaries and be
  individually testable in isolation.
- Dependencies MUST be provided via dependency injection; layers depend on abstractions, not
  concrete implementations, so exchange clients and stores can be mocked.
- All I/O MUST be async/await. Blocking on async calls (`.Result`, `.Wait()`) is prohibited.

**Rationale**: Independent layers let risk and strategy be tested and reasoned about separately,
and DI is what makes the mocked-client testing in Principle IV possible.

### VI. Observability & Auditability

Every financial event leaves a structured, auditable trace.

- Structured logging and metrics MUST be emitted on every order, fill, and error.
- All financial state changes MUST be auditable: reconstructable after the fact with enough
  context (timestamps, identifiers, amounts, decision inputs) to explain what happened and why.
- Observability MUST coexist with Principle I: traces carry identifiers and amounts, never raw
  secrets or sensitive payload fields.

**Rationale**: When real money moves, "what happened?" must always have a precise answer — for
debugging, for compliance, and for trust in the system's decisions.

### VII. Deterministic Financial Arithmetic

Money math is exact.

- All financial calculations (prices, quantities, balances, P&L, fees) MUST use `decimal`.
- `float` and `double` MUST NEVER be used for monetary or quantity values.

**Rationale**: Binary floating point cannot represent decimal fractions exactly; rounding drift
in prices and quantities corrupts balances and risk checks. `decimal` makes financial results
deterministic and correct.

## Technology & Security Constraints

- **Platform**: C# on .NET 10. Code uses modern C# language features and the async/await model
  throughout.
- **Secrets**: Sourced only from environment variables or a secrets manager (per Principle I).
  No secret material in the repository, build artifacts, or container images.
- **Exchange access**: All exchange clients are accessed through abstractions that support
  mocking, timeouts, retries with backoff, and idempotency keys (per Principles III, V).
- **Money type**: `decimal` is the only permitted type for monetary and quantity values
  (per Principle VII).
- **Trading mode**: Paper/dry-run is the default; live trading is gated behind explicit
  configuration (per Principle II).

## Development Workflow & Quality Gates

- **TDD gate**: Trading, risk, and order-execution changes MUST land with tests written first and
  passing, including edge-case coverage with mocked exchange clients.
- **Risk gate**: Any change touching an order path MUST demonstrate that risk checks
  (position size, daily loss, kill-switch) are enforced and cannot be bypassed in live mode.
- **Security gate**: Reviews MUST verify no secret is hardcoded or loggable and that order
  payloads are redacted before logging.
- **Determinism gate**: Reviews MUST reject `float`/`double` in financial calculations.
- **Review compliance**: Every pull request MUST verify compliance with this constitution.
  Violations block merge unless justified under Governance.

## Governance

This constitution supersedes other practices where they conflict. When guidance is silent, the
principles' intent (capital safety, security, correctness) governs.

- **Amendments**: Proposed via pull request that documents the change, its rationale, and any
  migration impact. Amendments take effect only when merged.
- **Versioning policy**: Semantic versioning applies to this document.
  - **MAJOR**: Backward-incompatible governance changes — removing or redefining a principle.
  - **MINOR**: Adding a principle or section, or materially expanding guidance.
  - **PATCH**: Clarifications, wording, and non-semantic refinements.
- **Compliance review**: All pull requests and reviews MUST verify compliance with these
  principles. Any exception to a NON-NEGOTIABLE principle is prohibited; exceptions to other
  principles MUST be justified in writing in the PR and approved by a maintainer.

**Version**: 1.0.0 | **Ratified**: 2026-06-25 | **Last Amended**: 2026-06-25
