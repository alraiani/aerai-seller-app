namespace AERai.Web.Application.Settlements;

/// <summary>Order of settlements on the Settlements list.</summary>
public enum SettlementSort
{
    /// <summary>Newest period end first.</summary>
    PeriodEnd = 0,

    /// <summary>Largest sales first.</summary>
    Sales = 1,

    /// <summary>Largest fees first (fees are negative, so the most negative first).</summary>
    Fees = 2,

    /// <summary>Largest refunds first (refunds are negative, so the most negative first).</summary>
    Refunds = 3,

    /// <summary>Largest net payout first.</summary>
    Net = 4,
}
