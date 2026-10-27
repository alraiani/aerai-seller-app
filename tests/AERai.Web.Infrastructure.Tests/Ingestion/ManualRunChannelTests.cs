using AERai.Web.Application.Ingestion;
using AERai.Web.Infrastructure.Ingestion;

namespace AERai.Web.Infrastructure.Tests.Ingestion;

public sealed class ManualRunChannelTests
{
    [Fact]
    public async Task CancelQueued_DropsWhatHasNotStartedAndLeavesTheInboxWorking()
    {
        using var channel = new ManualRunChannel();
        await channel.EnqueueAsync(new ManualRunRequest(1, "ops"), CancellationToken.None);
        await channel.EnqueueAsync(new ManualRunRequest(2, "ops"), CancellationToken.None);

        Assert.Equal(2, channel.QueuedCount);
        Assert.Equal(2, channel.CancelQueued());
        Assert.Equal((0, 0), (channel.QueuedCount, channel.CancelQueued()));

        // A request queued afterwards is still handed to the scheduler.
        await channel.EnqueueAsync(new ManualRunRequest(3, "ops"), CancellationToken.None);
        Assert.Equal(new ManualRunRequest(3, "ops"), await channel.DequeueAsync(TimeSpan.FromSeconds(1), CancellationToken.None));
    }
}
