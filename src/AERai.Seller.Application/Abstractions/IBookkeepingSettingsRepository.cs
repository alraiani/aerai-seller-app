using AERai.Seller.Domain;

namespace AERai.Seller.Application.Abstractions;

public interface IBookkeepingSettingsRepository
{
    /// <summary>Returns the single settings row, creating a default (empty) one on first access.</summary>
    Task<BookkeepingSettings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(BookkeepingSettings settings, CancellationToken cancellationToken = default);
}
