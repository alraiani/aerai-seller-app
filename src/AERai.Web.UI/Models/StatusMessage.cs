namespace AERai.Web.UI.Models;

/// <summary>
/// TempData keys for the Post/Redirect/Get status banner rendered by <c>_StatusMessage</c>.
/// </summary>
public static class StatusMessage
{
    /// <summary>Key for a success message.</summary>
    public const string Success = "Status.Success";

    /// <summary>Key for a warning: the action went ahead, but the user should know something about it.</summary>
    public const string Warning = "Status.Warning";

    /// <summary>Key for an error message.</summary>
    public const string Error = "Status.Error";
}
