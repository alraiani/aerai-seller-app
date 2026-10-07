using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IAmazonCatalogGateway"/>: pictures are registered per ASIN.</summary>
internal sealed class FakeAmazonCatalogGateway : IAmazonCatalogGateway
{
    public Dictionary<string, byte[]> Pictures { get; } = new(StringComparer.Ordinal);

    public HashSet<string> FailingDownloads { get; } = new(StringComparer.Ordinal);

    public Exception? LookupFailure { get; set; }

    public List<string> LookedUp { get; } = [];

    public int Downloads { get; private set; }

    public Task<IReadOnlyDictionary<string, Uri>> FindMainImagesAsync(Marketplace marketplace, IReadOnlyCollection<string> asins, CancellationToken cancellationToken)
    {
        if (LookupFailure is not null)
        {
            throw LookupFailure;
        }

        LookedUp.AddRange(asins);
        IReadOnlyDictionary<string, Uri> found = asins.Where(Pictures.ContainsKey).ToDictionary(a => a, a => new Uri($"https://m.media-amazon.com/images/I/{a}.png"));
        return Task.FromResult(found);
    }

    public Task<Stream> OpenImageAsync(Uri url, CancellationToken cancellationToken)
    {
        Downloads++;
        var asin = Path.GetFileNameWithoutExtension(url.AbsolutePath);
        return FailingDownloads.Contains(asin)
            ? throw new HttpRequestException("404 Not Found")
            : Task.FromResult<Stream>(new MemoryStream(Pictures[asin]));
    }
}
