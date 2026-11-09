using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ProductCostHistory"/> to <c>core.ProductCostHistory</c>.</summary>
internal sealed class ProductCostHistoryConfiguration : IEntityTypeConfiguration<ProductCostHistory>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductCostHistory> builder)
    {
        builder.ToTable("ProductCostHistory", Schemas.Core, t => t.HasCheckConstraint("CK_ProductCostHistory_NonNegative", "[CostOfGoods] IS NULL OR [CostOfGoods] >= 0"));
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Sku).HasMaxLength(64);
        builder.Property(h => h.MarketplaceId).HasMaxLength(16);
        builder.Property(h => h.CostOfGoods).HasPrecision(18, 2);
        builder.Property(h => h.ChangedBy).HasMaxLength(256);

        // Profit looks up "the latest change on or before a date" per (marketplace, SKU).
        builder.HasIndex(h => new { h.MarketplaceId, h.Sku, h.EffectiveFrom }).IncludeProperties(h => h.CostOfGoods);
        builder.HasOne<Product>().WithMany().HasForeignKey(h => h.Sku).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(h => h.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
