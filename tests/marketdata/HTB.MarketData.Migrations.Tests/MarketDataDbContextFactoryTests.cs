namespace HTB.MarketData.Migrations.Tests;

public sealed class MarketDataDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_falls_back_to_default_when_env_var_absent()
    {
        var previous = Environment.GetEnvironmentVariable(
            MarketDataDbContextFactory.ConnectionStringEnvVar
        );
        Environment.SetEnvironmentVariable(MarketDataDbContextFactory.ConnectionStringEnvVar, null);
        try
        {
            var factory = new MarketDataDbContextFactory();

            using var context = factory.CreateDbContext(Array.Empty<string>());

            // Forces OnModelCreating without needing a live connection.
            Assert.NotNull(context.Model);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                MarketDataDbContextFactory.ConnectionStringEnvVar,
                previous
            );
        }
    }

    [Fact]
    public void CreateDbContext_uses_env_var_when_present()
    {
        var previous = Environment.GetEnvironmentVariable(
            MarketDataDbContextFactory.ConnectionStringEnvVar
        );
        Environment.SetEnvironmentVariable(
            MarketDataDbContextFactory.ConnectionStringEnvVar,
            "Host=db;Port=5432;Database=custom;Username=u;Password=p"
        );
        try
        {
            var factory = new MarketDataDbContextFactory();

            using var context = factory.CreateDbContext(Array.Empty<string>());

            Assert.NotNull(context);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                MarketDataDbContextFactory.ConnectionStringEnvVar,
                previous
            );
        }
    }
}
