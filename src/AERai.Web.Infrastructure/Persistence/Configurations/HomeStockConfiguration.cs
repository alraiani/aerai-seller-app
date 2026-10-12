using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="HomeStock"/> to <c>core.HomeStock</c>.</summary>
internal sealed class HomeStockConfiguration : IEntityTypeConfiguration<HomeStock>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<HomeStock> builder)
    {
        builder.ToTable("HomeStock", Schemas.Core, t => t.HasCheckConstraint("CK_HomeStock_NonNegative", "[Quantity] >= 0"));
        builder.HasKey(h => new { h.Sku, h.MarketplaceId });
        builder.Property(h => h.Sku).HasMaxLength(64);
        builder.Property(h => h.MarketplaceId).HasMaxLength(16);
        builder.Property(h => h.UpdatedBy).HasMaxLength(256);
        builder.HasOne<Product>().WithMany().HasForeignKey(h => h.Sku).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(h => h.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
