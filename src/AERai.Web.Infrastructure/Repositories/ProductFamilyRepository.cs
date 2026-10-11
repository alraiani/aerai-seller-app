using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IProductFamilyRepository"/>. Name uniqueness is the
/// database's unique index (case-insensitive collation), so concurrent creates cannot duplicate.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class ProductFamilyRepository(AppDbContext dbContext) : IProductFamilyRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<FamilySummary>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.ProductFamilies.AsNoTracking()
            .OrderBy(f => f.Name)
            .Select(f => new FamilySummary(f.Id, f.Name, dbContext.Products.Count(p => p.FamilyId == f.Id)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public Task<int?> FindAsync(string name, CancellationToken cancellationToken) =>
        dbContext.ProductFamilies.Where(f => f.Name == name).Select(f => (int?)f.Id).SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<int?> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var family = new ProductFamily { Name = name };
        dbContext.ProductFamilies.Add(family);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return family.Id;
        }
        catch (DbUpdateException ex) when (IsDuplicateName(ex))
        {
            dbContext.Entry(family).State = EntityState.Detached;
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<FamilyWriteOutcome> RenameAsync(int id, string name, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await dbContext.ProductFamilies.Where(f => f.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(f => f.Name, name), cancellationToken)
                .ConfigureAwait(false);
            return updated == 0 ? FamilyWriteOutcome.NotFound : FamilyWriteOutcome.Saved;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return FamilyWriteOutcome.Duplicate;
        }
    }

    /// <inheritdoc/>
    public async Task<string?> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var name = await dbContext.ProductFamilies.Where(f => f.Id == id).Select(f => f.Name).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (name is null)
        {
            return null;
        }

        // The foreign key is ON DELETE SET NULL, so the family's products simply become unassigned.
        await dbContext.ProductFamilies.Where(f => f.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        return name;
    }

    /// <inheritdoc/>
    public async Task<int> AssignAsync(IReadOnlyCollection<string> skus, int? familyId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skus);

        var updated = 0;

        // Chunked so a large selection stays well under SQL Server's 2,100-parameter limit.
        foreach (var chunk in skus.Chunk(1000))
        {
            updated += await dbContext.Products.Where(p => chunk.Contains(p.Sku))
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.FamilyId, familyId), cancellationToken)
                .ConfigureAwait(false);
        }

        return updated;
    }

    private static bool IsDuplicateName(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };
}
