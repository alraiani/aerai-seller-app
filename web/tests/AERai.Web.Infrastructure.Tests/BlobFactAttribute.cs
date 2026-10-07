namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// A fact that runs only when <c>AERAI_TEST_BLOB</c> holds a Blob Storage connection string
/// (e.g. <c>UseDevelopmentStorage=true</c> with Azurite running). Skipped otherwise.
/// </summary>
public sealed class BlobFactAttribute : FactAttribute
{
    /// <summary>Environment variable holding the storage connection string.</summary>
    public const string EnvironmentVariable = "AERAI_TEST_BLOB";

    public BlobFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
        {
            Skip = $"Set {EnvironmentVariable} to run Blob Storage integration tests.";
        }
    }
}
