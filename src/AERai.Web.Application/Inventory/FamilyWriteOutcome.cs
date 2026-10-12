namespace AERai.Web.Application.Inventory;

/// <summary>What happened when a family was created or renamed.</summary>
public enum FamilyWriteOutcome
{
    /// <summary>Saved.</summary>
    Saved = 0,

    /// <summary>The family does not exist (rename only).</summary>
    NotFound = 1,

    /// <summary>Another family already has that name (names are unique, ignoring case).</summary>
    Duplicate = 2,
}
