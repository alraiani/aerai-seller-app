using AERai.Web.Application;
using AERai.Web.Application.Abstractions;
using AERai.Web.Infrastructure.Storage;
using Azure.Storage.Blobs;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// Creates a uniquely named, fully migrated database for a test class and drops it afterwards.
/// Services are wired exactly as in production (<c>AddApplication</c> + <c>AddInfrastructure</c>);
/// the raw store is real Blob Storage when <c>AERAI_TEST_BLOB</c> is set, otherwise in-memory.
/// </summary>
public sealed class SqlDatabaseFixture : IAsyncLifetime
{
    private readonly string? _serverConnectionString = Environment.GetEnvironmentVariable(SqlFactAttribute.EnvironmentVariable);
    private ServiceProvider? _services;
    private bool _usesBlob;

    /// <summary>The root provider; create a scope per unit of work.</summary>
    public IServiceProvider Services => _services ?? throw new InvalidOperationException("SQL tests are not configured.");

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_serverConnectionString))
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(_serverConnectionString)
        {
            InitialCatalog = $"AERaiTest_{Guid.NewGuid():N}",
        };

        var blobConnectionString = Environment.GetEnvironmentVariable(BlobFactAttribute.EnvironmentVariable);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sql"] = builder.ConnectionString,

                // The emulator connection string never connects unless used; the in-memory store replaces it below.
                ["ConnectionStrings:RawStorage"] = blobConnectionString ?? "UseDevelopmentStorage=true",
                ["RawStorage:ContainerName"] = $"test-{Guid.NewGuid():N}",

                // The built-in simulator stands in for Amazon, so ingestion runs end to end without credentials.
                ["SpApi:Mode"] = "Simulated",
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddApplication()
            .AddInfrastructure(configuration);

        if (string.IsNullOrWhiteSpace(blobConnectionString))
        {
            services.AddSingleton<IRawFileStore, InMemoryRawFileStore>();
        }

        _services = services.BuildServiceProvider();
        _usesBlob = !string.IsNullOrWhiteSpace(blobConnectionString);
        if (_usesBlob)
        {
            await _services.InitializeRawStorageAsync();
        }

        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_services is null)
        {
            return;
        }

        await using (var scope = _services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }

        if (_usesBlob)
        {
            await _services.GetRequiredService<BlobContainerClient>().DeleteIfExistsAsync();
        }

        await _services.DisposeAsync();
    }
}
