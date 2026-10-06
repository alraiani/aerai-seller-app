using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IProductFamilyRepository"/>.</summary>
internal sealed class FakeProductFamilyRepository : IProductFamilyRepository
{
    private int _nextId = 1;

    public Dictionary<int, string> Families { get; } = [];

    /// <summary>Family id per SKU (SKUs not in the dictionary don't exist).</summary>
    public Dictionary<string, int?> Skus { get; } = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<FamilySummary>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FamilySummary>>(Families
            .Select(f => new FamilySummary(f.Key, f.Value, Skus.Values.Count(v => v == f.Key)))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());

    public Task<int?> FindAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(Families.Where(f => string.Equals(f.Value, name, StringComparison.OrdinalIgnoreCase)).Select(f => (int?)f.Key).FirstOrDefault());

    public Task<int?> CreateAsync(string name, CancellationToken cancellationToken)
    {
        if (Families.Values.Any(v => string.Equals(v, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult<int?>(null);
        }

        var id = _nextId++;
        Families[id] = name;
        return Task.FromResult<int?>(id);
    }

    public Task<FamilyWriteOutcome> RenameAsync(int id, string name, CancellationToken cancellationToken)
    {
        if (!Families.ContainsKey(id))
        {
            return Task.FromResult(FamilyWriteOutcome.NotFound);
        }

        if (Families.Any(f => f.Key != id && string.Equals(f.Value, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(FamilyWriteOutcome.Duplicate);
        }

        Families[id] = name;
        return Task.FromResult(FamilyWriteOutcome.Saved);
    }

    public Task<string?> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        if (!Families.Remove(id, out var name))
        {
            return Task.FromResult<string?>(null);
        }

        foreach (var sku in Skus.Where(s => s.Value == id).Select(s => s.Key).ToList())
        {
            Skus[sku] = null;
        }

        return Task.FromResult<string?>(name);
    }

    public Task<int> AssignAsync(IReadOnlyCollection<string> skus, int? familyId, CancellationToken cancellationToken)
    {
        var updated = 0;
        foreach (var sku in skus.Where(Skus.ContainsKey))
        {
            Skus[sku] = familyId;
            updated++;
        }

        return Task.FromResult(updated);
    }
}
