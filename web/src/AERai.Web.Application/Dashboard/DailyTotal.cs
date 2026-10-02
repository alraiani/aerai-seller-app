namespace AERai.Web.Application.Dashboard;

/// <summary>
/// All-SKU sales for one day, used by the dashboard trend.
/// </summary>
/// <param name="Date">UTC date.</param>
/// <param name="Units">Units sold.</param>
/// <param name="Revenue">Revenue.</param>
public sealed record DailyTotal(DateOnly Date, int Units, decimal Revenue);
