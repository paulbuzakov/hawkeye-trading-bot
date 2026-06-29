namespace HTB.Strategy.Loader.Persistence;

/// <summary>Whether saving a definition created a new version row or refreshed an existing one.</summary>
public enum StrategySaveOutcome
{
    Inserted,
    Updated,
}
