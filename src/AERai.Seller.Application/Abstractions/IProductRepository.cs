using AERai.Seller.Domain;

namespace AERai.Seller.Application.Abstractions;

public interface IProductRepository
{
    Task UpsertAsync(Product product, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);
}
