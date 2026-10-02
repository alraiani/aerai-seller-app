using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="OrderItem"/> to <c>core.OrderItem</c>.</summary>
internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItem", Schemas.Core);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Sku).HasMaxLength(64);
        builder.Property(i => i.ItemPrice).HasPrecision(18, 2);
        builder.HasIndex(i => new { i.OrderId, i.Sku }).IsUnique();
        builder.HasIndex(i => i.Sku);
        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.Sku).OnDelete(DeleteBehavior.Restrict);
    }
}
