using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class BackfillSplitterTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(30, 1)]
    [InlineData(31, 2)]
    [InlineData(60, 2)]
    [InlineData(283, 10)]
    public void Split_CutsIntoWindowsOfAtMostMaxDays(int days, int expectedWindows)
    {
        var whole = new BackfillWindow(Start, Start.AddDays(days));

        var windows = BackfillSplitter.Split(whole, 30);

        Assert.Equal(expectedWindows, windows.Count);
        Assert.All(windows, w => Assert.True(w.Span <= TimeSpan.FromDays(30)));
    }

    [Fact]
    public void Split_WindowsJoinEndToStartAndCoverTheWholeRange()
    {
        var whole = new BackfillWindow(Start, Start.AddDays(100).AddHours(5));

        var windows = BackfillSplitter.Split(whole, 30);

        Assert.Equal(whole.Start, windows[0].Start);
        Assert.Equal(whole.End, windows[^1].End);
        for (var i = 1; i < windows.Count; i++)
        {
            Assert.Equal(windows[i - 1].End, windows[i].Start);
        }
    }

    [Fact]
    public void Split_MaxDaysBelowOne_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => BackfillSplitter.Split(new BackfillWindow(Start, Start.AddDays(5)), 0));
}
