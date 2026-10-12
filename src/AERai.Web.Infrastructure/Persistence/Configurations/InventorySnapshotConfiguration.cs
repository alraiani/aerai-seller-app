using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="InventorySnapshot"/> to <c>core.InventorySnapshot</c>.</summary>
internal sealed class InventorySnapshotConfiguration : IEntityTypeConfiguration<InventorySnapshot>
{
    /// <summary>Allows exactly the states in <see cref="InventoryStates.All"/>.</summary>
    private static readonly string StateCheckSql =
        $"[State] IN ({string.Join(", ", InventoryStates.All.Select(s => $"N'{s}'"))})";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InventorySnapshot> builder)
    {
        builder.ToTable("InventorySnapshot", Schemas.Core, t =>
            t.HasCheckConstraint("CK_InventorySnapshot_State", StateCheckSql));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Sku).HasMaxLength(64);
        builder.Property(s => s.State).HasMaxLength(32);
        builder.Property(s => s.MarketplaceId).HasMaxLength(16);

        // Each marketplace has its own fulfillment network, so the same SKU has a separate snapshot per marketplace.
        builder.HasIndex(s => new { s.MarketplaceId, s.Sku, s.SnapshotDate, s.State }).IsUnique();
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(s => s.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(s => s.Sku).OnDelete(DeleteBehavior.Restrict);
    }
}
