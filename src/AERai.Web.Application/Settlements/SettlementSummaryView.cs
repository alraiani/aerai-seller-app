namespace AERai.Web.Application.Settlements;

/// <summary>Which columns a settlement's per-SKU summary shows.</summary>
public enum SettlementSummaryView
{
    /// <summary>Sales, refunds, expenses, and net: the payout at a glance.</summary>
    Sales = 0,

    /// <summary>Refunds and expenses by category, with other expenses split by kind of charge.</summary>
    Expenses = 1,
}
