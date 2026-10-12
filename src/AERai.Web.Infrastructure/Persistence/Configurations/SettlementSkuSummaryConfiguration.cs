using AERai.Web.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="SettlementSkuSummary"/> to <c>rpt.vw_SettlementSkuSummary</c> (read-only, keyless).</summary>
internal sealed class SettlementSkuSummaryConfiguration : IEntityTypeConfiguration<SettlementSkuSummary>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SettlementSkuSummary> builder)
    {
        builder.ToView("vw_SettlementSkuSummary", Schemas.Reporting).HasNoKey();
        builder.Property(v => v.MarketplaceId).HasMaxLength(16);
        builder.Property(v => v.SalePrice).HasPrecision(18, 2);
        builder.Property(v => v.Sales).HasPrecision(18, 2);
        builder.Property(v => v.Fees).HasPrecision(18, 2);
        builder.Property(v => v.Refunds).HasPrecision(18, 2);
        builder.Property(v => v.Other).HasPrecision(18, 2);
        builder.Property(v => v.Net).HasPrecision(18, 2);
    }
}
