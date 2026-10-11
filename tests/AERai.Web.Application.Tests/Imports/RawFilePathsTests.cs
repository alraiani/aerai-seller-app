using AERai.Web.Application.Imports;
using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Tests.Imports;

public sealed class RawFilePathsTests
{
    [Fact]
    public void Build_PartitionsBySourceAndUtcDate()
    {
        // 23:30 at UTC-7 is already the next day in UTC; partitions use UTC.
        var receivedAt = new DateTimeOffset(2026, 10, 1, 23, 30, 0, TimeSpan.FromHours(-7));
        var id = Guid.Parse("0123456789abcdef0123456789abcdef");

        var path = RawFilePaths.Build(ImportSource.Settlements, receivedAt, id, "Settlement Sept.tsv");

        Assert.Equal("settlements/2026/10/02/0123456789abcdef0123456789abcdef-Settlement_Sept.tsv", path);
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("orders (1).csv", "orders__1_.csv")]
    [InlineData("übersicht.csv", "_bersicht.csv")]
    [InlineData("...", "file")]
    [InlineData("", "file")]
    public void SanitizeFileName_RemovesUnsafeCharacters(string input, string expected) =>
        Assert.Equal(expected, RawFilePaths.SanitizeFileName(input));
}
