using AERai.Seller.Domain;

namespace AERai.Seller.Application.Abstractions;

public interface IBookkeepingExportRepository
{
    Task InsertAsync(BookkeepingExportRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookkeepingExportRecord>> GetAllAsync(CancellationToken cancellationToken = default);
}
