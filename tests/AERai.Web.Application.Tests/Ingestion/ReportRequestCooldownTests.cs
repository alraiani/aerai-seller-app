using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class ReportRequestCooldownTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WaitUntil_ReportWithoutCooldown_IsNull() =>
        Assert.Null(ReportRequestCooldown.WaitUntil(AmazonReportType.RestockRecommendations, Now.AddMinutes(-1), Now));

    [Fact]
    public void WaitUntil_NeverRequested_IsNull() =>
        Assert.Null(ReportRequestCooldown.WaitUntil(AmazonReportType.FbaReservedInventory, null, Now));

    [Fact]
    public void WaitUntil_InsideTheCooldown_IsTheLastRequestPlusCooldownAndMargin() =>
        Assert.Equal(Now.AddMinutes(21), ReportRequestCooldown.WaitUntil(AmazonReportType.FbaReservedInventory, Now.AddMinutes(-10), Now));

    [Fact]
    public void WaitUntil_AfterTheCooldownAndMargin_IsNull() =>
        Assert.Null(ReportRequestCooldown.WaitUntil(AmazonReportType.FbaInventory, Now.AddMinutes(-31), Now));

    [Fact]
    public void Describe_SaysUntilWhenInUtcAndWhy() =>
        Assert.Equal(
            "Waiting until 14:42 UTC to ask Amazon: it builds this report at most once every 30 minutes.",
            ReportRequestCooldown.Describe(AmazonReportType.FbaInventory, new DateTimeOffset(2026, 10, 15, 10, 42, 0, TimeSpan.FromHours(-4))));
}
