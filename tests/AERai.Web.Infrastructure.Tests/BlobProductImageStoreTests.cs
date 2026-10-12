using AERai.Web.Application.Abstractions;
using AERai.Web.Infrastructure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// Tests <see cref="IProductImageStore"/> against real Blob Storage (Azurite), using throwaway
/// containers wired through the production <c>AddInfrastructure</c> registration.
/// </summary>
public sealed class BlobProductImageStoreTests : IAsyncLifetime
{
    private readonly string? _connectionString = Environment.GetEnvironmentVariable(BlobFactAttribute.EnvironmentVariable);
    private ServiceProvider? _services;

    private IProductImageStore Store => _services!.GetRequiredService<IProductImageStore>();

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
    public async Task SaveOpenDelete_RoundTripsAndKeepsContentType()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];

        await Store.SaveAsync("abc.png", new MemoryStream(png), "image/png", CancellationToken.None);
        await using (var stream = await Store.OpenReadAsync("abc.png", CancellationToken.None))
        {
            Assert.NotNull(stream);
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            Assert.Equal(png, copy.ToArray());
        }

        var properties = await _services!.GetRequiredService<ProductImageContainer>().Client.GetBlobClient("abc.png").GetPropertiesAsync();
        Assert.Equal("image/png", properties.Value.ContentType);

        await Store.DeleteAsync("abc.png", CancellationToken.None);
        Assert.Null(await Store.OpenReadAsync("abc.png", CancellationToken.None));
        await Store.DeleteAsync("abc.png", CancellationToken.None); // deleting twice is harmless
    }
}
