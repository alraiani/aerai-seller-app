using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="SettlementLine"/> to <c>core.SettlementLine</c>.</summary>
internal sealed class SettlementLineConfiguration : IEntityTypeConfiguration<SettlementLine>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SettlementLine> builder)
    {
        builder.ToTable("SettlementLine", Schemas.Core);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.TransactionType).HasMaxLength(64);
        builder.Property(l => l.AmazonOrderId).HasMaxLength(32);
        builder.Property(l => l.OrderItemCode).HasMaxLength(64);

        // No FK to Product: settlement amounts can reference SKUs that were never imported via orders.
        builder.Property(l => l.Sku).HasMaxLength(64);
        builder.Property(l => l.AmountType).HasMaxLength(64);
        builder.Property(l => l.AmountDescription).HasMaxLength(128);
        builder.Property(l => l.Amount).HasPrecision(18, 2);

        // The Orders page looks up each listed order's settlement lines by order id.
        builder.HasIndex(l => l.AmazonOrderId);

        // Everything reads lines one settlement at a time (the summary-table refresh at promotion, the
        // Summary and Lines pages), so the settlement index carries every column they read.
        builder.HasIndex(l => l.SettlementId)
            .IncludeProperties(l => new { l.PostedDate, l.TransactionType, l.AmazonOrderId, l.OrderItemCode, l.Quantity, l.Sku, l.AmountType, l.AmountDescription, l.Amount });
    }
}
