using HTB.Strategy.Shared.Domain;

namespace HTB.Strategy.Loader.Persistence;

public interface IStrategyDefinitionRepository
{
    /// <summary>
    /// Persists <paramref name="definition"/> keyed by its version id (<c>id + version</c>):
    /// inserts a new row, or refreshes the descriptive fields of an existing version. Returns
    /// which of the two happened.
    /// </summary>
    Task<StrategySaveOutcome> SaveAsync(StrategyDefinition definition, CancellationToken cancellationToken = default);
}
