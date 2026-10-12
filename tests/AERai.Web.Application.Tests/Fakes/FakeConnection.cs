using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>Configurable <see cref="IAmazonConnectionInfo"/>.</summary>
internal sealed class FakeConnection(bool canRun = true) : IAmazonConnectionInfo
{
    public string Mode => canRun ? "Simulated" : "Disabled";

    public bool CanRun => canRun;

    public string? Problem => canRun ? null : "SP-API is disabled.";
}

/// <summary><see cref="IManualRunChannel"/> that records enqueued requests.</summary>
internal sealed class FakeManualRunChannel : IManualRunChannel
{
    public List<ManualRunRequest> Enqueued { get; } = [];

    public int Wakes { get; private set; }

    public void Wake() => Wakes++;

    public ValueTask EnqueueAsync(ManualRunRequest request, CancellationToken cancellationToken)
    {
        Enqueued.Add(request);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ManualRunRequest?> DequeueAsync(TimeSpan wait, CancellationToken cancellationToken) => throw new NotSupportedException();
}
