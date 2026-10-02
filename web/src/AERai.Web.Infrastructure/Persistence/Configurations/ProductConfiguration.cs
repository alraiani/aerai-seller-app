using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Product"/> to <c>core.Product</c>.</summary>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Product", Schemas.Core);
        builder.HasKey(p => p.Sku);
        builder.Property(p => p.Sku).HasMaxLength(64);
        builder.Property(p => p.Asin).HasMaxLength(16);
        builder.Property(p => p.Title).HasMaxLength(400);
        builder.Property(p => p.CostOfGoods).HasPrecision(18, 2);
    }
}
