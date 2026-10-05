namespace AERai.Web.Application.Dashboard;

/// <summary>How urgently an attention item needs action.</summary>
public enum AttentionSeverity
{
    /// <summary>Losing money or data now (stockouts, failing syncs).</summary>
    Critical = 1,

    /// <summary>Will become a problem soon or blocks data from reaching reports.</summary>
    Warning = 2,

    /// <summary>Worth doing to get more out of the app.</summary>
    Info = 3,
}

/// <summary>Where an attention item's action lives; the UI maps each target to a page.</summary>
public enum AttentionTarget
{
    /// <summary>Inventory page.</summary>
    Inventory = 1,

    /// <summary>Products &amp; COGS page.</summary>
    Products = 2,

    /// <summary>Import batches tool.</summary>
    Batches = 3,

    /// <summary>Amazon sync schedules.</summary>
    Sync = 4,
}

/// <summary>Something on the dashboard that needs a person to act.</summary>
/// <param name="Severity">Urgency.</param>
/// <param name="Title">Short headline, e.g. "3 SKUs out of stock".</param>
/// <param name="Detail">One line of specifics, e.g. which SKUs.</param>
/// <param name="Target">Where to go to fix it.</param>
public sealed record AttentionItem(AttentionSeverity Severity, string Title, string Detail, AttentionTarget Target);
