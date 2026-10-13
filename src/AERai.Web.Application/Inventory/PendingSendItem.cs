namespace AERai.Web.Application.Inventory;

/// <summary>An open send pending to Amazon, as listed for people to check.</summary>
/// <param name="Send">The send.</param>
/// <param name="AgeDays">Whole days since it was logged.</param>
/// <param name="Overdue">Whether Amazon still doesn't show it after <see cref="InventoryOptions.PendingSendExpiryDays"/>.</param>
public sealed record PendingSendItem(PendingSend Send, int AgeDays, bool Overdue);
