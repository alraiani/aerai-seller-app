using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="InventoryHealth"/> to <c>core.InventoryHealth</c>.</summary>
internal sealed class InventoryHealthConfiguration : IEntityTypeConfiguration<InventoryHealth>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InventoryHealth> builder)
    {
        builder.ToTable("InventoryHealth", Schemas.Core, t => t.HasCheckConstraint(
            "CK_InventoryHealth_Units",
            "[Age0To90] >= 0 AND [Age91To180] >= 0 AND [Age181To270] >= 0 AND [Age271To365] >= 0 AND [Age365Plus] >= 0 AND [EstimatedExcess] >= 0"));
        builder.HasKey(h => new { h.MarketplaceId, h.Sku });
        builder.Property(h => h.MarketplaceId).HasMaxLength(16);
        builder.Property(h => h.Sku).HasMaxLength(64);
        builder.Property(h => h.StorageCostNextMonth).HasColumnType("decimal(18,2)");
        builder.Property(h => h.AgedSurchargeNextMonth).HasColumnType("decimal(18,2)");
        builder.Property(h => h.Currency).HasMaxLength(3);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(h => h.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(h => h.Sku).OnDelete(DeleteBehavior.Cascade);
    }
}
