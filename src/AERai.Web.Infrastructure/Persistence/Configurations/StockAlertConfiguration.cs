using AERai.Web.Domain.Alerts;
using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="StockAlert"/> to <c>ops.StockAlert</c>.</summary>
internal sealed class StockAlertConfiguration : IEntityTypeConfiguration<StockAlert>
{
    /// <summary>Name of the index that allows one open alert per (marketplace, SKU).</summary>
    internal const string OneOpenAlertIndex = "UX_StockAlert_Open";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StockAlert> builder)
    {
        builder.ToTable("StockAlert", Schemas.Operations);
        builder.HasKey(a => a.Id);
        builder.Property(a => a.MarketplaceId).HasMaxLength(16);
        builder.Property(a => a.Sku).HasMaxLength(64);
        builder.Property(a => a.Level).HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.Message).HasMaxLength(400);

        // Two instances refreshing at once cannot both open an alert for the same SKU.
        builder.HasIndex(a => new { a.MarketplaceId, a.Sku }).IsUnique().HasFilter("[ResolvedAt] IS NULL").HasDatabaseName(OneOpenAlertIndex);
        builder.HasIndex(a => new { a.MarketplaceId, a.ResolvedAt });

        builder.HasOne<Marketplace>().WithMany().HasForeignKey(a => a.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(a => a.Sku).OnDelete(DeleteBehavior.Cascade);
    }
}
