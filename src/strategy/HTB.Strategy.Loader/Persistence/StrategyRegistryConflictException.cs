namespace HTB.Strategy.Loader.Persistence;

/// <summary>
/// Thrown when a save would redefine an immutable strategy version: the natural key
/// <c>(strategy-id, version-number)</c> already exists with a different <c>rules-hash</c> and is no
/// longer a draft. A change to an active/retired version must be a new version number, never an edit.
/// </summary>
public sealed class StrategyRegistryConflictException(string message) : Exception(message);
