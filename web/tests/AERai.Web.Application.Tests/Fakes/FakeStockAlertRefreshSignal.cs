using AERai.Web.Application.Abstractions;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary><see cref="IStockAlertRefreshSignal"/> that counts requests.</summary>
internal sealed class FakeStockAlertRefreshSignal : IStockAlertRefreshSignal
{
    public int Requests { get; private set; }

    public void Request() => Requests++;
}
