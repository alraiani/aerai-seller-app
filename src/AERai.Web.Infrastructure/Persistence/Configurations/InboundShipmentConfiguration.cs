using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="InboundShipment"/> to <c>core.InboundShipment</c>.</summary>
internal sealed class InboundShipmentConfiguration : IEntityTypeConfiguration<InboundShipment>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InboundShipment> builder)
    {
        builder.ToTable("InboundShipment", Schemas.Core);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.MarketplaceId).HasMaxLength(16);

        // At most the ledger's reference length, so an accepted shipment's id always fits on its send.
        builder.Property(s => s.ShipmentConfirmationId).HasMaxLength(64);
        builder.Property(s => s.AmazonShipmentId).HasMaxLength(64);
        builder.Property(s => s.InboundPlanId).HasMaxLength(64);
        builder.Property(s => s.PlanName).HasMaxLength(256);
        builder.Property(s => s.Name).HasMaxLength(256);
        builder.Property(s => s.AmazonStatus).HasMaxLength(32);
        builder.Property(s => s.Review).HasConversion<string>().HasMaxLength(24);
        builder.Property(s => s.ReviewedBy).HasMaxLength(256);
        builder.HasMany(s => s.Items).WithOne().HasForeignKey(i => i.InboundShipmentId).OnDelete(DeleteBehavior.Cascade);

        // The sync upserts by Amazon's FBA id; the page, tab badge, and bell read a marketplace's shipments by review.
        builder.HasIndex(s => new { s.MarketplaceId, s.ShipmentConfirmationId }).IsUnique();
        builder.HasIndex(s => new { s.MarketplaceId, s.Review });
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(s => s.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
