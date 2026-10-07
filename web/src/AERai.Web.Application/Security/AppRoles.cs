namespace AERai.Web.Application.Security;

/// <summary>
/// Role names used for authorization. Seeded into Identity at startup.
/// </summary>
public static class AppRoles
{
    /// <summary>Full access, including user administration.</summary>
    public const string Admin = "Admin";

    /// <summary>Can import and promote data and edit product costs.</summary>
    public const string Operator = "Operator";

    /// <summary>Read-only access to dashboards and reports.</summary>
    public const string Viewer = "Viewer";

    /// <summary>All roles, from most to least privileged.</summary>
    public static IReadOnlyList<string> All { get; } = [Admin, Operator, Viewer];
}
