namespace AERai.Web.Application.Settlements;

/// <summary>What Amazon holds for a marketplace in one currency.</summary>
/// <param name="Currency">ISO 4217 currency.</param>
/// <param name="Amount">Total held (what will be paid out once released).</param>
/// <param name="Transactions">Deferred transactions.</param>
/// <param name="Orders">Distinct orders among them.</param>
/// <param name="NextMaturity">The soonest release date, when known.</param>
public sealed record DeferredHeld(string Currency, decimal Amount, int Transactions, int Orders, DateTimeOffset? NextMaturity);
