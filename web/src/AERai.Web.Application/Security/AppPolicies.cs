namespace AERai.Web.Application.Security;

/// <summary>
/// Authorization policy names. Policies are defined in the UI composition root; the names live
/// here so every layer refers to the same constants.
/// </summary>
public static class AppPolicies
{
    /// <summary>Admins only.</summary>
    public const string RequireAdmin = nameof(RequireAdmin);

    /// <summary>Operators and Admins.</summary>
    public const string RequireOperator = nameof(RequireOperator);
}
