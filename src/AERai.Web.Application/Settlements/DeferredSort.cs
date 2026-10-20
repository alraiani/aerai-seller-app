namespace AERai.Web.Application.Settlements;

/// <summary>Sorts for the Deferred page.</summary>
public enum DeferredSort
{
    /// <summary>By the date Amazon releases the money, soonest first.</summary>
    Maturity = 0,

    /// <summary>By amount, largest first.</summary>
    Amount = 1,

    /// <summary>By the date Amazon posted the transaction, newest first.</summary>
    Posted = 2,
}
