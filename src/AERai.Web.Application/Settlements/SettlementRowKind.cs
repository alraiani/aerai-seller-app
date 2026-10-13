namespace AERai.Web.Application.Settlements;

/// <summary>Which rows of a settlement's per-SKU summary to list.</summary>
public enum SettlementRowKind
{
    /// <summary>Every row.</summary>
    All = 0,

    /// <summary>Rows with any refund amount.</summary>
    WithRefunds = 1,

    /// <summary>The row of amounts that belong to no SKU (service fees, subscriptions, adjustments).</summary>
    NoSku = 2,
}
