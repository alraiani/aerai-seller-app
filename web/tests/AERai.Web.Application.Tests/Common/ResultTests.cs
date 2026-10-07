using AERai.Web.Application.Common;

namespace AERai.Web.Application.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Value_OnFailure_Throws()
    {
        var result = Result.Failure<int>("nope");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_WithEmptyMessage_Throws() =>
        Assert.Throws<ArgumentException>(() => Result.Failure(string.Empty));

    [Theory]
    [InlineData(0, 25, 0)]
    [InlineData(3, 25, 50)]
    [InlineData(1, 10_000, 0)]
    public void PageRequest_NormalizesPagingValues(int page, int size, int expectedSkip)
    {
        var request = new PageRequest(page, size);

        Assert.Equal(expectedSkip, request.Skip);
        Assert.InRange(request.SafePageSize, 1, PageRequest.MaxPageSize);
    }
}
