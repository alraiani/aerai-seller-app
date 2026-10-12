using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="HomeStockMovement"/> to <c>core.HomeStockMovement</c>.</summary>
internal sealed class HomeStockMovementConfiguration : IEntityTypeConfiguration<HomeStockMovement>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<HomeStockMovement> builder)
    {
        builder.ToTable("HomeStockMovement", Schemas.Core, t =>
        {
            t.HasCheckConstraint("CK_HomeStockMovement_Units", "[Units] <> 0");
            t.HasCheckConstraint("CK_HomeStockMovement_BalanceAfter", "[BalanceAfter] >= 0");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MarketplaceId).HasMaxLength(16);
        builder.Property(m => m.Sku).HasMaxLength(64);

        // Stored as text so the ledger reads plainly in SQL and survives enum reordering.
        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(m => m.Reference).HasMaxLength(100);
        builder.Property(m => m.Note).HasMaxLength(500);
        builder.Property(m => m.CreatedBy).HasMaxLength(256);

        // The ledger page lists a marketplace's entries newest first, optionally for one SKU.
        builder.HasIndex(m => new { m.MarketplaceId, m.Sku, m.Id });
        builder.HasIndex(m => new { m.MarketplaceId, m.OccurredAt });

        // One reversal per entry, so an entry can't be undone twice.
        builder.HasIndex(m => m.ReversesId).IsUnique().HasFilter("[ReversesId] IS NOT NULL");
        builder.HasOne<HomeStockMovement>().WithMany().HasForeignKey(m => m.ReversesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(m => m.Sku).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(m => m.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
