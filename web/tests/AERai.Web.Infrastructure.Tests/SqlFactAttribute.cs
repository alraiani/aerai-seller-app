namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// A fact that runs only when <c>AERAI_TEST_SQL</c> holds a SQL Server connection string (without a
/// database name; each test class creates and drops its own database). Skipped otherwise, so the
/// suite stays green on machines and CI agents without SQL Server.
/// </summary>
public sealed class SqlFactAttribute : FactAttribute
{
    /// <summary>Environment variable holding the server connection string.</summary>
    public const string EnvironmentVariable = "AERAI_TEST_SQL";

    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
        {
            Skip = $"Set {EnvironmentVariable} to run SQL Server integration tests.";
        }
    }
}
