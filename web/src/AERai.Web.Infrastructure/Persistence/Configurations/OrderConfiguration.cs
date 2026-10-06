using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Order"/> to <c>core.[Order]</c>.</summary>
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Order", Schemas.Core);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.AmazonOrderId).HasMaxLength(32);
        builder.HasIndex(o => o.AmazonOrderId).IsUnique();
        builder.Property(o => o.OrderStatus).HasMaxLength(32);
        builder.Property(o => o.Currency).HasMaxLength(3);
        builder.Property(o => o.MarketplaceId).HasMaxLength(16);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(o => o.MarketplaceId).OnDelete(DeleteBehavior.Restrict);

        // SWITCHOFFSET + CAST are deterministic, so the column can be persisted and indexed.
        builder.Property(o => o.PurchaseDateUtc)
            .HasComputedColumnSql("CAST(SWITCHOFFSET([PurchaseDate], '+00:00') AS date)", stored: true);
        builder.HasIndex(o => o.PurchaseDateUtc);

        builder.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
