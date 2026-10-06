using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Abstractions;

/// <summary>Persistence for product families.</summary>
public interface IProductFamilyRepository
{
    /// <summary>Every family with its SKU count, by name.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The families.</returns>
    Task<IReadOnlyList<FamilySummary>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Finds a family by name, ignoring case.</summary>
    /// <param name="name">Normalized name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Its id, or <see langword="null"/>.</returns>
    Task<int?> FindAsync(string name, CancellationToken cancellationToken);

    /// <summary>Creates a family.</summary>
    /// <param name="name">Normalized, validated name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new id, or <see langword="null"/> when the name is taken.</returns>
    Task<int?> CreateAsync(string name, CancellationToken cancellationToken);

    /// <summary>Renames a family.</summary>
    /// <param name="id">Family id.</param>
    /// <param name="name">Normalized, validated name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What happened.</returns>
    Task<FamilyWriteOutcome> RenameAsync(int id, string name, CancellationToken cancellationToken);

    /// <summary>Deletes a family; its SKUs become unassigned.</summary>
    /// <param name="id">Family id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The deleted family's name, or <see langword="null"/> when it did not exist.</returns>
    Task<string?> DeleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>Puts SKUs in a family, or takes them out of any family.</summary>
    /// <param name="skus">SKUs to change.</param>
    /// <param name="familyId">Family id, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many SKUs exist and were updated.</returns>
    Task<int> AssignAsync(IReadOnlyCollection<string> skus, int? familyId, CancellationToken cancellationToken);
}
