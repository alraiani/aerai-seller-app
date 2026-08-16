using AERai.Seller.Application.Reports;
using AERai.Seller.Domain.Staging;
using Xunit;

namespace AERai.Seller.Application.Tests.Reports;

public class InventoryPlanningReportParserTests
{
    [Fact]
    public void Parse_MapsKnownColumns_ToPerStateSnapshots()
    {
        const string tsv =
            "sku\tavailable\tTotal Reserved Quantity\tunfulfillable-quantity\tfc-transfer\tReserved FC Processing\tinbound-working\tinbound-shipped\tinbound-received\n" +
            "WIDGET-1\t42\t3\t1\t6\t7\t5\t10\t2\n";

        var rows = TsvReportParser.Parse(tsv);
        var snapshotDate = new DateOnly(2026, 8, 1);
        var syncedAt = DateTimeOffset.UtcNow;

        var snapshots = InventoryPlanningReportParser.Parse(rows, snapshotDate, syncedAt);

        Assert.Equal(42, snapshots.Single(s => s.State == InventoryState.Available).Quantity);
        Assert.Equal(3, snapshots.Single(s => s.State == InventoryState.Reserved).Quantity);
        Assert.Equal(1, snapshots.Single(s => s.State == InventoryState.Unfulfillable).Quantity);
        Assert.Equal(6, snapshots.Single(s => s.State == InventoryState.FcTransfer).Quantity);
        Assert.Equal(7, snapshots.Single(s => s.State == InventoryState.FcProcessing).Quantity);
        Assert.Equal(17, snapshots.Single(s => s.State == InventoryState.Inbound).Quantity);
        Assert.All(snapshots, s => Assert.Equal("WIDGET-1", s.Sku));
        Assert.All(snapshots, s => Assert.Equal(snapshotDate, s.SnapshotDate));
    }

    [Fact]
    public void Parse_SkipsRows_WithoutSku()
    {
        const string tsv = "sku\tavailable\n\t5\n";

        var rows = TsvReportParser.Parse(tsv);
        var snapshots = InventoryPlanningReportParser.Parse(rows, new DateOnly(2026, 8, 1), DateTimeOffset.UtcNow);

        Assert.Empty(snapshots);
    }

    [Fact]
    public void Parse_OmitsZeroQuantityInboundState_WhenAllInboundColumnsMissing()
    {
        const string tsv = "sku\tavailable\nWIDGET-1\t10\n";

        var rows = TsvReportParser.Parse(tsv);
        var snapshots = InventoryPlanningReportParser.Parse(rows, new DateOnly(2026, 8, 1), DateTimeOffset.UtcNow);

        Assert.Single(snapshots);
        Assert.Equal(InventoryState.Available, snapshots[0].State);
    }
}
