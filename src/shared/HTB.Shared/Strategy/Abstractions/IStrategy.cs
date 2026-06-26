using HTB.Shared.Strategy.Domain;
using HTB.Shared.Strategy.Domain.Conditions;

namespace HTB.Shared.Strategy.Abstractions;

/// <summary>
/// A pure function of closed candles → <see cref="Signal"/> (the Strategy pattern). Same context in
/// ⇒ same signal out: no I/O, no clock, no randomness, no order placement.
/// </summary>
public interface IStrategy
{
    Signal Evaluate(EvaluationContext context);
}
