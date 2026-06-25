# Contract: Strategy Factory

**Namespace**: `HTB.Shared.Strategy` · **Satisfies**: FR-003, FR-003a · **Ref**: R3

The single creation path: resolve a configuration's named strategy type, validate its parameter
values against the type's spec, and instantiate the executable strategy. Reused by backtest and
future live execution.

```csharp
public interface IStrategyFactory
{
    // Validate parameter values against the named type's ParameterSpec.
    StrategyValidationResult Validate(string typeName, ParameterSet parameters);

    // Resolve the type and instantiate it with validated parameters.
    IStrategy Create(string typeName, ParameterSet parameters);
}
```

**Rules**
- An **unknown type name** ⇒ validation failure (fail closed, FR-003a).
- A parameter **outside its `ParameterDef` Min/Max**, of the wrong type, or referencing an unknown
  key ⇒ validation failure; missing optional keys take the spec's `Default`.
- Validation runs **before any candle data is read** (FR-012); the failure message identifies the
  offending type/parameter.
- `Create` is pure construction (no I/O); the resulting `IStrategy` is deterministic.
