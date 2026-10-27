using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests that the home-stock ledger always adds up to the balance.</summary>
public sealed class HomeStockLedgerTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);

    private async Task AddProductAsync(string sku)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(new Product { Sku = sku, Title = $"Title {sku}", CreatedAt = Now, UpdatedAt = Now });
        await db.SaveChangesAsync();
    }

    private static HomeStockLedgerWrite Write(string sku, HomeStockMovementType type, int units) =>
        new(MarketplaceIds.UnitedStates, sku, type, units, Now, "PO-1", null, null, Now, "tests");

    private async Task<(int Balance, int LedgerSum, int Entries)> StateAsync(string sku)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var balance = await db.HomeStocks.Where(h => h.Sku == sku && h.MarketplaceId == MarketplaceIds.UnitedStates).SumAsync(h => h.Quantity);
        var entries = await db.HomeStockMovements.Where(m => m.Sku == sku && m.MarketplaceId == MarketplaceIds.UnitedStates).ToListAsync();
        return (balance, entries.Sum(e => e.Units), entries.Count);
    }

    [SqlFact]
    public async Task EveryKindOfWrite_KeepsTheLedgerEqualToTheBalance()
    {
        await AddProductAsync("T-LED-1");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();
        var items = scope.ServiceProvider.GetRequiredService<IInventoryItemRepository>();

        var (received, receivedId) = await ledger.RecordAsync(Write("T-LED-1", HomeStockMovementType.ReceivedFromSupplier, 100), CancellationToken.None);
        await ledger.RecordAsync(Write("T-LED-1", HomeStockMovementType.ShippedToAmazon, -40), CancellationToken.None);
        await items.SetHomeStockAsync(MarketplaceIds.UnitedStates, [new HomeStockEntry("T-LED-1", 55)], Now, "tests", CancellationToken.None);
        var (recount, _) = await ledger.RecordAsync(Write("T-LED-1", HomeStockMovementType.CountCorrection, 55), CancellationToken.None);

        Assert.Equal(HomeStockLedgerOutcome.Recorded, received);
        Assert.Equal(HomeStockLedgerOutcome.Unchanged, recount);
        Assert.Equal((55, 55, 3), await StateAsync("T-LED-1"));

        // Reversing the receipt would need 100 units back out, but only 55 are left.
        var (reverse, _) = await ledger.ReverseAsync(MarketplaceIds.UnitedStates, receivedId!.Value, Now, "tests", CancellationToken.None);
        Assert.Equal(HomeStockLedgerOutcome.WouldGoNegative, reverse);
    }

    [SqlFact]
    public async Task Reverse_UndoesOnceAndListsBothEntries()
    {
        await AddProductAsync("T-LED-2");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();

        var (_, id) = await ledger.RecordAsync(Write("T-LED-2", HomeStockMovementType.ReceivedFromSupplier, 20), CancellationToken.None);
        var (first, _) = await ledger.ReverseAsync(MarketplaceIds.UnitedStates, id!.Value, Now, "tests", CancellationToken.None);
        var (second, _) = await ledger.ReverseAsync(MarketplaceIds.UnitedStates, id.Value, Now, "tests", CancellationToken.None);

        Assert.Equal((HomeStockLedgerOutcome.Recorded, HomeStockLedgerOutcome.CannotReverse), (first, second));
        Assert.Equal((0, 0, 2), await StateAsync("T-LED-2"));

        var page = await ledger.ListAsync(MarketplaceIds.UnitedStates, new HomeStockLedgerFilter(Sku: "T-LED-2"), new PageRequest(), CancellationToken.None);
        Assert.Equal([(-20, 0), (20, 20)], page.Items.Select(e => (e.Units, e.BalanceAfter)));
        Assert.False(page.Items[1].CanReverse);
        Assert.Equal(page.Items[0].Id, page.Items[1].ReversedById);
    }

    [SqlFact]
    public async Task Record_ShippingMoreThanOnHand_LeavesNothingBehind()
    {
        await AddProductAsync("T-LED-3");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();

        var (outcome, _) = await ledger.RecordAsync(Write("T-LED-3", HomeStockMovementType.ShippedToAmazon, -1), CancellationToken.None);

        Assert.Equal(HomeStockLedgerOutcome.WouldGoNegative, outcome);
        Assert.Equal((0, 0, 0), await StateAsync("T-LED-3"));
    }

    [SqlFact]
    public async Task ApplyCounts_LogsEachDirectionWithItsTypeAndSkipsStaleBalances()
    {
        await AddProductAsync("T-CNT-UP");
        await AddProductAsync("T-CNT-DOWN");
        await AddProductAsync("T-CNT-STALE");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();
        await ledger.RecordAsync(Write("T-CNT-DOWN", HomeStockMovementType.ReceivedFromSupplier, 50), CancellationToken.None);
        await ledger.RecordAsync(Write("T-CNT-STALE", HomeStockMovementType.ReceivedFromSupplier, 9), CancellationToken.None);

        // T-CNT-STALE was reviewed at 0 but is 9 now, so its reviewed difference no longer holds.
        var (applied, stale) = await ledger.ApplyCountsAsync(
            MarketplaceIds.UnitedStates,
            [new("T-CNT-UP", 0, 30), new("T-CNT-DOWN", 50, 20), new("T-CNT-STALE", 0, 40)],
            HomeStockMovementType.ReceivedFromSupplier,
            HomeStockMovementType.ShippedToAmazon,
            Write(string.Empty, HomeStockMovementType.CountCorrection, 0),
            new Dictionary<string, AmazonBaseline> { ["T-CNT-DOWN"] = new(12, null) },
            CancellationToken.None);

        Assert.Equal(["T-CNT-UP", "T-CNT-DOWN"], applied.Select(a => a.Sku));
        Assert.Equal(["T-CNT-STALE"], stale);
        Assert.Equal((30, 30, 1), await StateAsync("T-CNT-UP"));
        Assert.Equal((20, 20, 2), await StateAsync("T-CNT-DOWN"));
        Assert.Equal((9, 9, 1), await StateAsync("T-CNT-STALE"));

        var types = await scope.ServiceProvider.GetRequiredService<AppDbContext>().HomeStockMovements
            .Where(m => m.Sku == "T-CNT-UP" || (m.Sku == "T-CNT-DOWN" && m.Units < 0))
            .OrderBy(m => m.Sku)
            .Select(m => new { m.Type, m.Units, m.Reference })
            .ToListAsync();
        Assert.Equal(
            [(HomeStockMovementType.ShippedToAmazon, -30, "PO-1"), (HomeStockMovementType.ReceivedFromSupplier, 30, "PO-1")],
            types.Select(t => (t.Type, t.Units, t.Reference)));
    }

    [SqlFact]
    public async Task ListActions_GroupsEachSaveNewestFirstWithTotalsAndItsNewestEntries()
    {
        await AddProductAsync("T-GRP-A");
        await AddProductAsync("T-GRP-B");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();
        foreach (var (sku, type, units, hour) in new[]
        {
            ("T-GRP-A", HomeStockMovementType.ReceivedFromSupplier, 20, 0),
            ("T-GRP-B", HomeStockMovementType.ReceivedFromSupplier, 10, 0),
            ("T-GRP-A", HomeStockMovementType.ShippedToAmazon, -5, 1),
            ("T-GRP-B", HomeStockMovementType.ShippedToAmazon, -3, 1),
            ("T-GRP-A", HomeStockMovementType.ShippedToAmazon, -2, 1),
            ("T-GRP-A", HomeStockMovementType.Other, 4, 2),
            ("T-GRP-A", HomeStockMovementType.CountCorrection, 16, 2),
        })
        {
            await ledger.RecordAsync(Write(sku, type, units) with { CreatedAt = Now.AddHours(hour) }, CancellationToken.None);
        }

        // Other test methods share the database, so the search keeps to this test's SKUs.
        var page = await ledger.ListActionsAsync(MarketplaceIds.UnitedStates, new HomeStockLedgerFilter(), new PageRequest(1, 2, "T-GRP-"), 2, CancellationToken.None);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal([Now.AddHours(2), Now.AddHours(1)], page.Items.Select(a => a.LoggedAt));
        var mixed = page.Items[0];
        Assert.Equal([HomeStockMovementType.CountCorrection, HomeStockMovementType.Other], mixed.Types);
        Assert.Equal((4, -1, 3), (mixed.UnitsIn, mixed.UnitsOut, mixed.NetUnits)); // +4 other, then a recount from 17 to 16.
        var shipped = page.Items[1];
        Assert.Equal((3, 2, 0, -10), (shipped.EntryCount, shipped.SkuCount, shipped.UnitsIn, shipped.UnitsOut));
        Assert.Equal([("T-GRP-A", -2), ("T-GRP-B", -3)], shipped.Entries.Select(e => (e.Sku, e.Units)));
        Assert.True(shipped.IsCapped);

        var onlyB = await ledger.ListActionsAsync(MarketplaceIds.UnitedStates, new HomeStockLedgerFilter(Sku: "T-GRP-B", Type: HomeStockMovementType.ShippedToAmazon), new PageRequest(), 10, CancellationToken.None);
        Assert.Equal([(Now.AddHours(1), 1)], onlyB.Items.Select(a => (a.LoggedAt, a.EntryCount)));
    }

    [SqlFact]
    public async Task ReverseAction_UndoesTheWholeSaveOrNothing()
    {
        await AddProductAsync("T-UNDO-A");
        await AddProductAsync("T-UNDO-B");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();

        // Its own user, since the class's other tests log at the same time and would join its actions.
        HomeStockLedgerWrite Undo(string sku, HomeStockMovementType type, int units) => Write(sku, type, units) with { CreatedBy = "undo-test" };
        var (_, first) = await ledger.RecordAsync(Undo("T-UNDO-A", HomeStockMovementType.ReceivedFromSupplier, 20), CancellationToken.None);
        await ledger.RecordAsync(Undo("T-UNDO-B", HomeStockMovementType.ReceivedFromSupplier, 10), CancellationToken.None);
        var (_, send) = await ledger.RecordAsync(Undo("T-UNDO-A", HomeStockMovementType.ShippedToAmazon, -4), CancellationToken.None);
        var (_, later) = await ledger.RecordAsync(Undo("T-UNDO-B", HomeStockMovementType.ShippedToAmazon, -6) with { CreatedAt = Now.AddHours(1) }, CancellationToken.None);

        // The first save received and sent at one logged time; the send lists as an action of its own.
        var listed = await ledger.ListActionsAsync(MarketplaceIds.UnitedStates, new HomeStockLedgerFilter(Sku: "T-UNDO-A"), new PageRequest(), 10, CancellationToken.None);
        Assert.Equal(
            [[HomeStockMovementType.ReceivedFromSupplier], [HomeStockMovementType.ShippedToAmazon]],
            listed.Items.OrderBy(a => a.Types[0]).Select(a => a.Types.ToList()));

        // Undoing the received part while the sends stand would take A to -4 and B to -6, so nothing is written.
        var (blocked, _, skus) = await ledger.ReverseActionAsync(MarketplaceIds.UnitedStates, first!.Value, Now.AddHours(2), "undoer", CancellationToken.None);
        Assert.Equal(HomeStockLedgerOutcome.WouldGoNegative, blocked);
        Assert.Equal(["T-UNDO-A", "T-UNDO-B"], skus.Order());
        Assert.Equal((16, 16, 2), await StateAsync("T-UNDO-A"));

        // Undoing the send reverses only the send, never the units received in the same save.
        var (sendOutcome, sendReversed, _) = await ledger.ReverseActionAsync(MarketplaceIds.UnitedStates, send!.Value, Now.AddHours(2), "undoer", CancellationToken.None);
        Assert.Equal((HomeStockLedgerOutcome.Recorded, 1), (sendOutcome, sendReversed));
        Assert.Equal((20, 20, 3), await StateAsync("T-UNDO-A"));

        await ledger.ReverseActionAsync(MarketplaceIds.UnitedStates, later!.Value, Now.AddHours(2), "undoer", CancellationToken.None);
        var (outcome, reversed, _) = await ledger.ReverseActionAsync(MarketplaceIds.UnitedStates, first.Value, Now.AddHours(3), "undoer", CancellationToken.None);

        // The received part held A +20 and B +10 (same logged time), so both are reversed.
        Assert.Equal((HomeStockLedgerOutcome.Recorded, 2), (outcome, reversed));
        Assert.Equal((0, 0, 4), await StateAsync("T-UNDO-A"));
        Assert.Equal((0, 0, 4), await StateAsync("T-UNDO-B"));
        var (again, _, _) = await ledger.ReverseActionAsync(MarketplaceIds.UnitedStates, first.Value, Now.AddHours(4), "undoer", CancellationToken.None);
        Assert.Equal(HomeStockLedgerOutcome.CannotReverse, again);
    }

    private static readonly IReadOnlyDictionary<string, AmazonBaseline> NoBaselines = new Dictionary<string, AmazonBaseline>();

    [SqlFact]
    public async Task Correct_ReplacesTheEntryInTheListsAndKeepsTheHistory()
    {
        await AddProductAsync("T-EDIT-1");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();
        var (_, id) = await ledger.RecordAsync(Write("T-EDIT-1", HomeStockMovementType.ReceivedFromSupplier, 20), CancellationToken.None);

        var (outcome, copyId) = await ledger.CorrectAsync(MarketplaceIds.UnitedStates, id!.Value, HomeStockMovementType.Other, 15, "found 15", NoBaselines, Now.AddHours(1), "editor", CancellationToken.None);
        var (again, _) = await ledger.CorrectAsync(MarketplaceIds.UnitedStates, id.Value, HomeStockMovementType.Other, 12, "x", NoBaselines, Now.AddHours(2), "editor", CancellationToken.None);
        var (copyAgain, secondId) = await ledger.CorrectAsync(MarketplaceIds.UnitedStates, copyId!.Value, HomeStockMovementType.Other, 12, "found 12", NoBaselines, Now.AddHours(3), "editor", CancellationToken.None);

        Assert.Equal((HomeStockLedgerOutcome.Recorded, HomeStockLedgerOutcome.CannotReverse, HomeStockLedgerOutcome.Recorded), (outcome, again, copyAgain));

        // Original, its reversal, the first copy, its reversal, and the second copy: all kept, adding up to the balance.
        Assert.Equal((12, 12, 5), await StateAsync("T-EDIT-1"));

        var page = await ledger.ListAsync(MarketplaceIds.UnitedStates, new HomeStockLedgerFilter(Sku: "T-EDIT-1"), new PageRequest(), CancellationToken.None);
        var entry = Assert.Single(page.Items);
        Assert.Equal((secondId, HomeStockMovementType.Other, 12, "found 12", "tests", Now), (entry.Id, entry.Type, entry.Units, entry.Note, entry.CreatedBy, entry.CreatedAt));
        Assert.Equal(new HomeStockLedgerEdit(Now.AddHours(3), "editor", HomeStockMovementType.Other, 15, "found 15"), entry.Edit);

        // By id, the history is still there (for a bulk edit to find), but only in its own marketplace.
        Assert.Equal([id.Value, copyId.Value], (await ledger.GetEntriesAsync(MarketplaceIds.UnitedStates, [id.Value, copyId.Value], CancellationToken.None)).Select(e => e.Id).Order());
        Assert.Empty(await ledger.GetEntriesAsync(MarketplaceIds.Canada, [id.Value], CancellationToken.None));

        // The copy keeps the original's logged time, so it stays in the original's action; the edit's reversals are hidden.
        var actions = await ledger.ListActionsAsync(MarketplaceIds.UnitedStates, new HomeStockLedgerFilter(Sku: "T-EDIT-1"), new PageRequest(), 10, CancellationToken.None);
        Assert.Equal([(Now, 1, 12, 0)], actions.Items.Select(a => (a.LoggedAt, a.EntryCount, a.UnitsIn, a.UnitsOut)));
    }

    [SqlFact]
    public async Task Correct_RefusesWhatWouldGoNegativeOrUndoWhatAmazonShows()
    {
        await AddProductAsync("T-EDIT-2");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();
        var (_, received) = await ledger.RecordAsync(Write("T-EDIT-2", HomeStockMovementType.ReceivedFromSupplier, 30), CancellationToken.None);
        var (_, sent) = await ledger.RecordAsync(Write("T-EDIT-2", HomeStockMovementType.ShippedToAmazon, -25) with { AmazonBaseline = new AmazonBaseline(40, Now) }, CancellationToken.None);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.HomeStockMovements.Where(m => m.Id == sent).ExecuteUpdateAsync(u => u.SetProperty(m => m.ReconciledUnits, 4));

        await CorrectAsync(HomeStockLedgerOutcome.WouldGoNegative, received!.Value, HomeStockMovementType.ReceivedFromSupplier, 20);
        await CorrectAsync(HomeStockLedgerOutcome.AlreadyAtAmazon, sent!.Value, HomeStockMovementType.ShippedToAmazon, -3);
        await CorrectAsync(HomeStockLedgerOutcome.AlreadyAtAmazon, sent.Value, HomeStockMovementType.CountCorrection, -25);
        var copyId = await CorrectAsync(HomeStockLedgerOutcome.Recorded, sent.Value, HomeStockMovementType.ShippedToAmazon, -6);

        // The corrected send keeps its baseline and Amazon's progress, and stays open for the 2 units still on their way.
        var copy = await db.HomeStockMovements.AsNoTracking().SingleAsync(m => m.Id == copyId);
        Assert.Equal((40, (int?)4, (DateTimeOffset?)null, Now), (copy.AmazonBaselineUnits ?? 0, (int?)copy.ReconciledUnits, copy.ReconciledAt, copy.CreatedAt));
        Assert.Equal((24, 24, 4), await StateAsync("T-EDIT-2"));

        async Task<long?> CorrectAsync(HomeStockLedgerOutcome expected, long id, HomeStockMovementType type, int units)
        {
            var (outcome, newId) = await ledger.CorrectAsync(MarketplaceIds.UnitedStates, id, type, units, null, NoBaselines, Now.AddHours(1), "editor", CancellationToken.None);
            Assert.Equal(expected, outcome);
            return newId;
        }
    }

    [SqlFact]
    public async Task Correct_EntryBecomingASend_IsLoggedAsOfTheEditWithTodaysBaseline()
    {
        await AddProductAsync("T-EDIT-3");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();
        await ledger.RecordAsync(Write("T-EDIT-3", HomeStockMovementType.ReceivedFromSupplier, 30), CancellationToken.None);
        var (_, other) = await ledger.RecordAsync(Write("T-EDIT-3", HomeStockMovementType.Other, -6) with { Note = "gave away" }, CancellationToken.None);
        var editedAt = Now.AddDays(2);
        var baselines = new Dictionary<string, AmazonBaseline> { ["T-EDIT-3"] = new(17, editedAt.AddHours(-1)) };

        var (outcome, copyId) = await ledger.CorrectAsync(MarketplaceIds.UnitedStates, other!.Value, HomeStockMovementType.ShippedToAmazon, -6, null, baselines, editedAt, "editor", CancellationToken.None);

        Assert.Equal(HomeStockLedgerOutcome.Recorded, outcome);
        var copy = await scope.ServiceProvider.GetRequiredService<AppDbContext>().HomeStockMovements.AsNoTracking().SingleAsync(m => m.Id == copyId);
        Assert.Equal((editedAt, "editor", (int?)17, Now), (copy.CreatedAt, copy.CreatedBy, copy.AmazonBaselineUnits, copy.OccurredAt));
    }

    [SqlFact]
    public async Task Correct_ReceiptWhoseUnitsPartlyWentOut_ShowsTheFinalBalance()
    {
        await AddProductAsync("T-EDIT-4");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();
        var (_, received) = await ledger.RecordAsync(Write("T-EDIT-4", HomeStockMovementType.ReceivedFromSupplier, 50), CancellationToken.None);
        await ledger.RecordAsync(Write("T-EDIT-4", HomeStockMovementType.ShippedToAmazon, -8), CancellationToken.None);

        // Taking the 50 out first would go to -8, so the copy is written first; it must still show the count after the edit.
        var (outcome, copyId) = await ledger.CorrectAsync(MarketplaceIds.UnitedStates, received!.Value, HomeStockMovementType.ReceivedFromSupplier, 50, "restock", NoBaselines, Now.AddHours(1), "editor", CancellationToken.None);

        Assert.Equal(HomeStockLedgerOutcome.Recorded, outcome);
        var page = await ledger.ListAsync(MarketplaceIds.UnitedStates, new HomeStockLedgerFilter(Sku: "T-EDIT-4"), new PageRequest(), CancellationToken.None);
        Assert.Equal(42, page.Items.Single(e => e.Id == copyId).BalanceAfter);
        Assert.Equal((42, 42, 4), await StateAsync("T-EDIT-4"));
    }

    [SqlFact]
    public async Task FixCorrectionBalances_SwapsOnlyCopiesStillShowingTheInBetweenCount()
    {
        await AddProductAsync("T-EDIT-5");
        await AddProductAsync("T-EDIT-6");
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IHomeStockLedgerRepository>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // T-EDIT-5: copy written first (8 units had gone out). T-EDIT-6: reversal written first, already right.
        var (_, fifty) = await ledger.RecordAsync(Write("T-EDIT-5", HomeStockMovementType.ReceivedFromSupplier, 50), CancellationToken.None);
        await ledger.RecordAsync(Write("T-EDIT-5", HomeStockMovementType.ShippedToAmazon, -8), CancellationToken.None);
        var (_, twenty) = await ledger.RecordAsync(Write("T-EDIT-6", HomeStockMovementType.ReceivedFromSupplier, 20), CancellationToken.None);
        var (_, copy5) = await ledger.CorrectAsync(MarketplaceIds.UnitedStates, fifty!.Value, HomeStockMovementType.ReceivedFromSupplier, 50, "restock", NoBaselines, Now.AddHours(1), "editor", CancellationToken.None);
        var (_, copy6) = await ledger.CorrectAsync(MarketplaceIds.UnitedStates, twenty!.Value, HomeStockMovementType.ReceivedFromSupplier, 15, null, NoBaselines, Now.AddHours(1), "editor", CancellationToken.None);

        // Put T-EDIT-5 back the way edits made before the fix left it: the copy shows the in-between 92.
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE core.HomeStockMovement SET BalanceAfter = 92 WHERE Sku = 'T-EDIT-5' AND CorrectsId IS NOT NULL;
            UPDATE core.HomeStockMovement SET BalanceAfter = 42 WHERE Sku = 'T-EDIT-5' AND ReversesId IS NOT NULL;
            """);
        var script = SqlResource.Read("V020_LedgerCorrectionBalances", "fix-correction-balances.sql");

        await db.Database.ExecuteSqlRawAsync(script);
        await db.Database.ExecuteSqlRawAsync(script);

        var balances = await db.HomeStockMovements.AsNoTracking()
            .Where(m => m.Id == copy5 || m.Id == copy6 || m.ReversesId == fifty || m.ReversesId == twenty)
            .ToDictionaryAsync(m => m.Id, m => m.BalanceAfter);
        var reversal5 = await db.HomeStockMovements.Where(m => m.ReversesId == fifty).Select(m => m.Id).SingleAsync();
        var reversal6 = await db.HomeStockMovements.Where(m => m.ReversesId == twenty).Select(m => m.Id).SingleAsync();
        Assert.Equal((42, 92), (balances[copy5!.Value], balances[reversal5]));
        Assert.Equal((15, 0), (balances[copy6!.Value], balances[reversal6]));
        Assert.Equal((42, 42, 4), await StateAsync("T-EDIT-5"));
    }
}
