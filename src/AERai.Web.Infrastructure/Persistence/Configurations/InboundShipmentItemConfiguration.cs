using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="InboundShipmentItem"/> to <c>core.InboundShipmentItem</c>.</summary>
internal sealed class InboundShipmentItemConfiguration : IEntityTypeConfiguration<InboundShipmentItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InboundShipmentItem> builder)
    {
        builder.ToTable("InboundShipmentItem", Schemas.Core, t => t.HasCheckConstraint("CK_InboundShipmentItem_Quantity", "Quantity >= 0"));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Sku).HasMaxLength(64);

        // Amazon lists a SKU once per shipment; the sync adds up repeats before writing.
        builder.HasIndex(i => new { i.InboundShipmentId, i.Sku }).IsUnique();
    }
}
