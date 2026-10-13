using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class FailureReasonTests
{
    [Fact]
    public void FromDocument_JsonWithErrorDetails_ReturnsTheDetails() =>
        Assert.Equal("Too many requests.", FailureReason.FromDocument("""{ "errorDetails": "Too   many\nrequests." }"""));

    [Fact]
    public void FromDocument_PlainText_ReturnsTheTextCollapsed() =>
        Assert.Equal("No inventory found.", FailureReason.FromDocument("  No inventory\r\n found.  "));

    [Fact]
    public void FromDocument_JsonWithoutErrorDetails_ReturnsTheJsonText() =>
        Assert.Equal("""{"code":"X"}""", FailureReason.FromDocument("""{"code":"X"}"""));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n ")]
    public void FromDocument_Empty_ReturnsNull(string? document) =>
        Assert.Null(FailureReason.FromDocument(document));

    [Fact]
    public void FromDocument_Long_IsCutToTheLimit()
    {
        var reason = FailureReason.FromDocument(new string('x', 2_000));

        Assert.Equal(FailureReason.MaxLength, reason!.Length);
        Assert.EndsWith("…", reason, StringComparison.Ordinal);
    }
}
