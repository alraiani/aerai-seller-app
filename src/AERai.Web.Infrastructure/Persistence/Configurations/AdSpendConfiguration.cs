using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="AdSpend"/> to <c>core.AdSpend</c>.</summary>
internal sealed class AdSpendConfiguration : IEntityTypeConfiguration<AdSpend>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AdSpend> builder)
    {
        builder.ToTable("AdSpend", Schemas.Core);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.MarketplaceId).HasMaxLength(16);
        builder.Property(s => s.Sku).HasMaxLength(64);
        builder.Property(s => s.Asin).HasMaxLength(16);
        builder.Property(s => s.CampaignId).HasMaxLength(32);
        builder.Property(s => s.CampaignName).HasMaxLength(256);
        builder.Property(s => s.Cost).HasPrecision(18, 2);
        builder.Property(s => s.AttributedSales).HasPrecision(18, 2);
        builder.Property(s => s.Currency).HasMaxLength(3);

        // Also the index the page and each sync's range replace read by: (marketplace, day range).
        builder.HasIndex(s => new { s.MarketplaceId, s.Date, s.Sku, s.CampaignId }).IsUnique();
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(s => s.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(s => s.Sku).OnDelete(DeleteBehavior.Restrict);
    }
}
