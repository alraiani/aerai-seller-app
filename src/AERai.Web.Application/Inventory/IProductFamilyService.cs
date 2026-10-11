using AERai.Web.Application.Common;

namespace AERai.Web.Application.Inventory;

/// <summary>Use cases for managing product families and putting SKUs in them.</summary>
public interface IProductFamilyService
{
    /// <summary>Every family with its SKU count, by name.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The families.</returns>
    Task<IReadOnlyList<FamilySummary>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Creates a family.</summary>
    /// <param name="name">Name as typed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The saved (tidied) name, or why it was not created.</returns>
    Task<Result<string>> CreateAsync(string? name, CancellationToken cancellationToken);

    /// <summary>Renames a family.</summary>
    /// <param name="id">Family id.</param>
    /// <param name="name">New name as typed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The saved (tidied) name, or why it was not renamed.</returns>
    Task<Result<string>> RenameAsync(int id, string? name, CancellationToken cancellationToken);

    /// <summary>Deletes a family; its SKUs stay, unassigned.</summary>
    /// <param name="id">Family id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The deleted family's name, or "not found".</returns>
    Task<Result<string>> DeleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Puts SKUs in the named family (created if new), or takes them out of any family when the name
    /// is blank.
    /// </summary>
    /// <param name="skus">SKUs to change.</param>
    /// <param name="familyName">Family name as typed; blank to clear.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many SKUs were updated, or why nothing was.</returns>
    Task<Result<int>> AssignAsync(IReadOnlyCollection<string> skus, string? familyName, CancellationToken cancellationToken);
}
