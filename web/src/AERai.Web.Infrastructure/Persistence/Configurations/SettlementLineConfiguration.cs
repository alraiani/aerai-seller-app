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

        // No FK to Product: settlement amounts can reference SKUs that were never imported via orders.
        builder.Property(l => l.Sku).HasMaxLength(64);
        builder.Property(l => l.AmountType).HasMaxLength(64);
        builder.Property(l => l.AmountDescription).HasMaxLength(128);
        builder.Property(l => l.Amount).HasPrecision(18, 2);
    }
}
