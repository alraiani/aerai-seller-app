using System.Security.Cryptography;
using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Infrastructure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// Tests <see cref="IRawFileStore"/> against real Blob Storage (Azurite), using a throwaway
/// container per test class wired through the production <c>AddInfrastructure</c> registration.
/// </summary>
public sealed class BlobRawFileStoreTests : IAsyncLifetime
{
    private readonly string? _connectionString = Environment.GetEnvironmentVariable(BlobFactAttribute.EnvironmentVariable);
    private ServiceProvider? _services;

    private IRawFileStore Store => _services!.GetRequiredService<IRawFileStore>();

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Placeholder: building the DbContext options never connects; these tests don't touch SQL.
                ["ConnectionStrings:Sql"] = "Server=unused;Database=unused",
                ["ConnectionStrings:RawStorage"] = _connectionString,
                ["RawStorage:ContainerName"] = $"test-{Guid.NewGuid():N}",
                ["RawStorage:ProductImageContainerName"] = $"test-img-{Guid.NewGuid():N}",
            })
            .Build();

        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
        await _services.InitializeRawStorageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.GetRequiredService<BlobContainerClient>().DeleteIfExistsAsync();
            await _services.GetRequiredService<ProductImageContainer>().Client.DeleteIfExistsAsync();
            await _services.DisposeAsync();
        }
    }

    [BlobFact]
    public async Task SaveAsync_ThenOpenReadAsync_RoundTripsBytesAndReturnsSha256()
    {
        var bytes = Encoding.UTF8.GetBytes("sku,quantity\nA-1,3\n");

        var hash = await Store.SaveAsync("orders/2026/10/02/x-orders.csv", new MemoryStream(bytes),
            new Dictionary<string, string> { ["source"] = "Orders" }, CancellationToken.None);

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), hash);

        await using var stream = await Store.OpenReadAsync("orders/2026/10/02/x-orders.csv", CancellationToken.None);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        Assert.Equal("sku,quantity\nA-1,3\n", await reader.ReadToEndAsync());
    }

    [BlobFact]
    public async Task SaveAsync_ExistingPath_NeverOverwrites()
    {
        const string path = "orders/2026/10/02/dup.csv";
        await Store.SaveAsync(path, new MemoryStream("first"u8.ToArray()), new Dictionary<string, string>(), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Store.SaveAsync(path, new MemoryStream("second"u8.ToArray()), new Dictionary<string, string>(), CancellationToken.None));

        await using var stream = await Store.OpenReadAsync(path, CancellationToken.None);
        using var reader = new StreamReader(stream!);
        Assert.Equal("first", await reader.ReadToEndAsync());
    }

    [BlobFact]
    public async Task OpenReadAsync_MissingPath_ReturnsNull() =>
        Assert.Null(await Store.OpenReadAsync("orders/nope.csv", CancellationToken.None));
}
