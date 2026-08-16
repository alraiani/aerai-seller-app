using AERai.Seller.Domain;

namespace AERai.Seller.Application.Abstractions;

public interface IBookkeepingAccountMappingRepository
{
    Task<IReadOnlyList<BookkeepingAccountMapping>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Upserts by (AmountType, AmountDescription).</summary>
    Task UpsertAsync(BookkeepingAccountMapping mapping, CancellationToken cancellationToken = default);
}
