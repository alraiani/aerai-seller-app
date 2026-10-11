using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ProductCost"/> to <c>core.ProductCost</c>.</summary>
internal sealed class ProductCostConfiguration : IEntityTypeConfiguration<ProductCost>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductCost> builder)
    {
        builder.ToTable("ProductCost", Schemas.Core, t => t.HasCheckConstraint("CK_ProductCost_NonNegative", "[CostOfGoods] >= 0"));
        builder.HasKey(c => new { c.Sku, c.MarketplaceId });
        builder.Property(c => c.Sku).HasMaxLength(64);
        builder.Property(c => c.MarketplaceId).HasMaxLength(16);
        builder.Property(c => c.CostOfGoods).HasPrecision(18, 2);
        builder.HasOne<Product>().WithMany().HasForeignKey(c => c.Sku).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(c => c.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
