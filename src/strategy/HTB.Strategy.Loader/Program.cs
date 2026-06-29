using System.Diagnostics.CodeAnalysis;

namespace HTB.Strategy.Loader;

/// <summary>
/// Console entry point for the strategy runner. This is a scaffold — pure composition wiring
/// will live here (real PostgreSQL + strategy execution), so it is excluded from coverage; the
/// testable logic will live in dedicated types alongside it.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class Program
{
    private static Task<int> Main(string[] args)
    {
        Console.WriteLine("HTB.Strategy.Loader — not yet implemented.");
        return Task.FromResult(0);
    }
}
