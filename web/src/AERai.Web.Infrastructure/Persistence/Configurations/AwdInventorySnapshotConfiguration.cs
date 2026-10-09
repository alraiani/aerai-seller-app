using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="AwdInventorySnapshot"/> to <c>core.AwdInventorySnapshot</c>.</summary>
internal sealed class AwdInventorySnapshotConfiguration : IEntityTypeConfiguration<AwdInventorySnapshot>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AwdInventorySnapshot> builder)
    {
        builder.ToTable("AwdInventorySnapshot", Schemas.Core);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Sku).HasMaxLength(64);
        builder.Property(s => s.MarketplaceId).HasMaxLength(16);

        builder.HasIndex(s => new { s.MarketplaceId, s.Sku, s.SnapshotDate }).IsUnique();

        // The view picks each marketplace's latest AWD date, so lookups go by (marketplace, date).
        builder.HasIndex(s => new { s.MarketplaceId, s.SnapshotDate });
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(s => s.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(s => s.Sku).OnDelete(DeleteBehavior.Restrict);
    }
}
