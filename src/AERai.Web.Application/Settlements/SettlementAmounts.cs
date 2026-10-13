namespace AERai.Web.Application.Settlements;

/// <summary>Which currency the settlement pages show amounts in.</summary>
public enum SettlementAmounts
{
    /// <summary>The marketplace's currency (USD for the United States), converted at the settlement's rate.</summary>
    Home = 0,

    /// <summary>The currency the settlement was paid in, as Amazon reported it.</summary>
    Original = 1,
}
