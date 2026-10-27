using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class PendingSendGroupTests
{
    private static readonly DateTimeOffset Saved = new(2026, 10, 12, 15, 0, 0, TimeSpan.Zero);

    private static PendingSendItem Item(long id, string sku, DateTimeOffset loggedAt, string user = "ops@example.com", int units = 10, int reconciled = 0, string? reference = null, bool overdue = false) =>
        new(new PendingSend(id, sku, null, null, null, loggedAt, loggedAt, user, units, reconciled, null, reference, IsOpen: true), 3, overdue);

    [Fact]
    public void GroupBySend_GroupsSendsLoggedInOneSaveNewestFirst()
    {
        var groups = PendingSendGroup.GroupBySend(
        [
            Item(1, "A", Saved),
            Item(2, "B", Saved.AddMinutes(5)),
            Item(3, "C", Saved),
            Item(4, "D", Saved, user: "other@example.com"),
        ]);

        Assert.Equal([[2L], [1L, 3L], [4L]], groups.Select(g => g.Items.Select(i => i.Send.Id).ToArray()));
        Assert.Equal((Saved, "ops@example.com"), (groups[1].LoggedAt, groups[1].LoggedBy));
    }

    [Fact]
    public void Totals_AddUpTheSendsAndRollUpReferencesAndOverdue()
    {
        var group = PendingSendGroup.GroupBySend(
        [
            Item(1, "A", Saved, units: 10, reconciled: 4, reference: "FBA15K7Y3ZQX"),
            Item(2, "B", Saved, units: 6, reference: "fba15k7y3zqx", overdue: true),
            Item(3, "C", Saved, units: 5),
        ])[0];

        Assert.Equal((21, 17, true), (group.UnitsSent, group.Pending, group.Overdue));
        Assert.Equal(["FBA15K7Y3ZQX"], group.References);
        Assert.Equal([3L], group.NeedsShipmentId.Select(i => i.Send.Id));
    }
}
