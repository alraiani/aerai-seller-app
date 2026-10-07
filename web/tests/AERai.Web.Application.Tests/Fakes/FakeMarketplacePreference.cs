using AERai.Web.Application.Abstractions;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>Remembered marketplace held in memory instead of a cookie.</summary>
internal sealed class FakeMarketplacePreference : IMarketplacePreference
{
    public string? Stored { get; set; }

    public string? Read() => Stored;

    public void Write(string marketplaceId) => Stored = marketplaceId;
}
