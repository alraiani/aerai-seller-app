namespace AERai.Web.Application.Inventory;

/// <summary>
/// How a family's suggested quantities are rounded to the way the business actually ships and orders
/// (e.g. handkerchiefs go to Amazon in 5s and 10s and are ordered from the supplier in batches of 100+ in 10s).
/// Every field is optional; <see langword="null"/> leaves that quantity unrounded.
/// </summary>
/// <param name="SendMultiple">Sends are a multiple of this (e.g. 5).</param>
/// <param name="SendMultipleLarge">From <paramref name="SendLargeFrom"/> units up, sends are a multiple of this instead (e.g. 10).</param>
/// <param name="SendLargeFrom">The send size where <paramref name="SendMultipleLarge"/> takes over (e.g. 50).</param>
/// <param name="OrderMinimum">Supplier orders are at least this many units (e.g. 100).</param>
/// <param name="OrderMultiple">Supplier orders are a multiple of this (e.g. 10).</param>
public sealed record RestockRules(int? SendMultiple, int? SendMultipleLarge, int? SendLargeFrom, int? OrderMinimum, int? OrderMultiple)
{
    /// <summary>Largest value any rule may take.</summary>
    public const int MaxValue = 100_000;

    /// <summary>No rounding.</summary>
    public static RestockRules None { get; } = new(null, null, null, null, null);

    /// <summary>Whether any rule is set.</summary>
    public bool Any => SendMultiple is not null || SendMultipleLarge is not null || OrderMinimum is not null || OrderMultiple is not null;

    /// <summary>
    /// Rounds a send to the nearest multiple (at least one multiple when anything is wanted), then fits it into
    /// what is at home by rounding down; when home holds less than one multiple, all of it is sent.
    /// </summary>
    /// <param name="wanted">Units Amazon should get.</param>
    /// <param name="atHome">Units on hand.</param>
    /// <returns>The units to send and the rounded want (so the shortfall is measured against what would be sent).</returns>
    public (int Send, int Wanted) RoundSend(int wanted, int atHome)
    {
        if (wanted <= 0)
        {
            return (0, 0);
        }

        var step = SendStep(wanted);
        var rounded = step is { } s ? Math.Max(s, (int)Math.Round(wanted / (decimal)s, MidpointRounding.AwayFromZero) * s) : wanted;
        if (rounded <= atHome)
        {
            return (rounded, rounded);
        }

        var cap = Math.Max(0, atHome);
        var capStep = SendStep(cap);
        var fitted = capStep is { } c && cap >= c ? cap / c * c : cap;
        return (fitted, rounded);
    }

    /// <summary>Rounds a supplier order up to the multiple, and to at least the minimum.</summary>
    /// <param name="units">Units the plan wants.</param>
    /// <returns>The order quantity (0 stays 0).</returns>
    public int RoundOrder(int units)
    {
        if (units <= 0)
        {
            return 0;
        }

        var atLeast = Math.Max(units, OrderMinimum ?? 0);
        return OrderMultiple is { } m and > 0 ? (atLeast + m - 1) / m * m : atLeast;
    }

    /// <summary>Checks the rules are usable.</summary>
    /// <returns>Why they aren't, or <see langword="null"/>.</returns>
    public string? Validate()
    {
        int?[] all = [SendMultiple, SendMultipleLarge, SendLargeFrom, OrderMinimum, OrderMultiple];
        if (all.Any(v => v is < 1 or > MaxValue))
        {
            return $"Each rule must be a whole number from 1 to {MaxValue:N0}, or blank.";
        }

        if ((SendMultipleLarge is null) != (SendLargeFrom is null))
        {
            return "Give both the larger send multiple and the size it starts at, or neither.";
        }

        return null;
    }

    private int? SendStep(int units) =>
        SendMultipleLarge is { } large and > 0 && SendLargeFrom is { } from && units >= from ? large
        : SendMultiple is { } small and > 0 ? small
        : null;
}
