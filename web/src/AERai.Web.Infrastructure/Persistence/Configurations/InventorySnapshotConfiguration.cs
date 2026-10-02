using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="InventorySnapshot"/> to <c>core.InventorySnapshot</c>.</summary>
internal sealed class InventorySnapshotConfiguration : IEntityTypeConfiguration<InventorySnapshot>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InventorySnapshot> builder)
    {
        builder.ToTable("InventorySnapshot", Schemas.Core, t =>
            t.HasCheckConstraint("CK_InventorySnapshot_State", "[State] IN (N'Available', N'Inbound', N'Reserved', N'Unfulfillable')"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Sku).HasMaxLength(64);
        builder.Property(s => s.State).HasMaxLength(32);
        builder.HasIndex(s => new { s.Sku, s.SnapshotDate, s.State }).IsUnique();
        builder.HasOne<Product>().WithMany().HasForeignKey(s => s.Sku).OnDelete(DeleteBehavior.Restrict);
    }
}
