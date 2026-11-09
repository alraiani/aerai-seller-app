namespace AERai.Web.Domain.Reporting;

/// <summary>
/// What a profit &amp; loss amount counts toward (<c>rpt.vw_ProfitLine.Category</c>, stored by name). The settlement
/// categories, plus the amounts profit treats apart: billed advertising, the reserve, and transfers to the bank.
/// </summary>
public enum ProfitCategory
{
    /// <summary>Product charges and taxes (collected and withheld cancel), and reimbursements Amazon pays.</summary>
    Sales = 1,

    /// <summary>Product charges and taxes given back to customers.</summary>
    RefundedSales = 2,

    /// <summary>Everything else on a refund: fees Amazon gives back, its refund commission, chargebacks.</summary>
    RefundedExpenses = 3,

    /// <summary>Shipping to customers and into Amazon.</summary>
    Shipping = 4,

    /// <summary>Amazon's referral fees.</summary>
    Commission = 5,

    /// <summary>FBA fulfilment, storage, removal, and disposal fees.</summary>
    FbaFees = 6,

    /// <summary>Everything else: promotions, closing fees, subscriptions, unitemized amounts.</summary>
    OtherExpenses = 7,

    /// <summary>Ad charges Amazon billed (profit uses Ads API spend instead, where it has synced).</summary>
    Advertising = 8,

    /// <summary>Amazon's account-level reserve: held back and released, never profit.</summary>
    Reserve = 9,

    /// <summary>Money paid out to the bank: never profit.</summary>
    Transfer = 10,
}
