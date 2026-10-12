using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class PendingReconciliationTests
{
    private const string Sku = "FOB00BL";

    private static readonly DateTimeOffset Sent = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);

    /// <summary>The snapshot the sends were compared with when logged (received two hours before the first).</summary>
    private static readonly DateTimeOffset BaselineAt = Sent.AddHours(-2);

    private static readonly DateTimeOffset NextSnapshot = Sent.AddDays(1);

    private static PendingSend Send(long id, int units, DateTimeOffset? at = null, int reconciled = 0, int baseline = 100, string? reference = null, bool open = true) =>
        new(id, Sku, null, null, null, at ?? Sent, at ?? Sent, "ops@example.com", units, reconciled, new AmazonBaseline(baseline, BaselineAt), reference, open);

    private static AmazonStockReading Stock(int sellThrough, int inbound = 0, DateTimeOffset? receivedAt = null) =>
        new(sellThrough, inbound, receivedAt ?? NextSnapshot);

    private static UnitsSold Sale(DateTimeOffset at, int units) => new(Sku, at, units);

    [Fact]
    public void Match_NoId_ArrivalsMatchOldestFirstAndLeaveTheRestPending()
    {
        // Stock went 100 -> 120 while 6 sold: 26 units arrived, enough for the first send and 2 of the second.
        var sends = new[] { Send(1, 24), Send(2, 10, Sent.AddHours(1)) };

        var matches = PendingReconciliation.Match(sends, Stock(120), [Sale(Sent.AddHours(3), 6)]);

        Assert.Equal([new PendingSendMatch(1, 24, true), new PendingSendMatch(2, 2, false)], matches);
    }

    [Fact]
    public void Match_NoId_CountsSalesFromTheBaselineSnapshotUntilTheNewOne()
    {
        // Sold between the baseline snapshot and the send counts; sold after the new snapshot doesn't.
        var sends = new[] { Send(1, 24) };
        var sold = new[] { Sale(BaselineAt.AddMinutes(30), 4), Sale(NextSnapshot.AddHours(1), 50) };

        var matches = PendingReconciliation.Match(sends, Stock(120), sold);

        Assert.Equal([new PendingSendMatch(1, 24, true)], matches);
    }

    [Fact]
    public void Match_SnapshotReceivedBeforeTheSend_LeavesItPending()
    {
        var matches = PendingReconciliation.Match([Send(1, 24)], Stock(500, 500, Sent.AddMinutes(-1)), []);

        Assert.Empty(matches);
    }

    [Fact]
    public void Match_ShipmentId_InboundCoversIt_EvenWhenTheBaselineAlreadyHadIt()
    {
        // The shipment was created in Seller Central before the send was logged, so stock didn't move.
        var matches = PendingReconciliation.Match([Send(1, 24, baseline: 124, reference: "FBA15K7Y3ZQX")], Stock(124, inbound: 24), []);

        Assert.Equal([new PendingSendMatch(1, 24, true)], matches);
    }

    [Fact]
    public void Match_ShipmentId_PartlyInbound_MatchesWhatIsThere()
    {
        var matches = PendingReconciliation.Match([Send(1, 24, reference: "FBA15K7Y3ZQX")], Stock(110, inbound: 10), []);

        Assert.Equal([new PendingSendMatch(1, 10, false)], matches);
    }

    [Fact]
    public void Match_NoId_DoesNotTakeUnitsOfAShipmentWithAnId()
    {
        // 30 arrived: they could be the id'd shipment's, so the id-less send isn't matched with them.
        var sends = new[] { Send(1, 20), Send(2, 30, Sent.AddHours(1), reference: "FBA15K7Y3ZQX") };

        var onlyShipment = PendingReconciliation.Match(sends, Stock(130, inbound: 30), []);
        var both = PendingReconciliation.Match(sends, Stock(150, inbound: 50), []);

        Assert.Equal([new PendingSendMatch(2, 30, true)], onlyShipment);
        Assert.Equal([new PendingSendMatch(1, 20, true), new PendingSendMatch(2, 30, true)], both);
    }

    [Fact]
    public void Match_NoId_UnitsAlreadyMatchedAreNotCountedTwice()
    {
        var partly = new[] { Send(1, 20, reconciled: 15) };

        Assert.Empty(PendingReconciliation.Match(partly, Stock(115), []));
        Assert.Equal([new PendingSendMatch(1, 20, true)], PendingReconciliation.Match(partly, Stock(120), []));
    }

    [Fact]
    public void Match_ClosedSendsAreLeftAlone()
    {
        var sends = new[] { Send(1, 20, reconciled: 20, open: false) };

        Assert.Empty(PendingReconciliation.Match(sends, Stock(500, 500), []));
    }

    [Fact]
    public void Match_StockFell_MatchesNothing()
    {
        Assert.Empty(PendingReconciliation.Match([Send(1, 24)], Stock(90), []));
    }
}
