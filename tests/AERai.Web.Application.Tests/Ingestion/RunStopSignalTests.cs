using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class RunStopSignalTests
{
    [Fact]
    public void RequestStop_TrackedRun_CancelsItsTokenAndRecordsWho()
    {
        var signal = new RunStopSignal();
        using var tracked = signal.Track(7, CancellationToken.None);

        Assert.True(signal.RequestStop(7, "boss"));
        Assert.True(signal.RequestStop(7, "someone else"));

        Assert.True(tracked.Token.IsCancellationRequested);
        Assert.Equal("boss", tracked.StoppedBy);
    }

    [Fact]
    public void RequestStop_UntrackedOrFinishedRun_ReturnsFalse()
    {
        var signal = new RunStopSignal();
        var tracked = signal.Track(7, CancellationToken.None);
        tracked.Dispose();

        Assert.False(signal.RequestStop(7, "boss"));
        Assert.False(signal.RequestStop(8, "boss"));
    }

    [Fact]
    public void Dispose_OlderTracking_LeavesANewerOneOfTheSameRun()
    {
        var signal = new RunStopSignal();
        var first = signal.Track(7, CancellationToken.None);
        using var second = signal.Track(7, CancellationToken.None);

        first.Dispose();

        Assert.True(signal.RequestStop(7, "boss"));
        Assert.True(second.Token.IsCancellationRequested);
    }

    [Fact]
    public void Track_ShutdownToken_IsLinkedWithoutAStop()
    {
        using var shutdown = new CancellationTokenSource();
        using var tracked = new RunStopSignal().Track(7, shutdown.Token);

        shutdown.Cancel();

        Assert.True(tracked.Token.IsCancellationRequested);
        Assert.Null(tracked.StoppedBy);
    }
}
