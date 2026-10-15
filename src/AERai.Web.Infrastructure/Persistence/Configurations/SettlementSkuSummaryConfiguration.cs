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
        builder.Property(v => v.HomeCurrency).HasMaxLength(3);
        builder.Property(v => v.ExchangeRate).HasPrecision(19, 10);
        builder.Property(v => v.HomeSalePrice).HasPrecision(18, 2);
        builder.Property(v => v.HomeSales).HasPrecision(18, 2);
        builder.Property(v => v.HomeFees).HasPrecision(18, 2);
        builder.Property(v => v.HomeRefunds).HasPrecision(18, 2);
        builder.Property(v => v.HomeOther).HasPrecision(18, 2);
        builder.Property(v => v.HomeNet).HasPrecision(18, 2);
        builder.Property(v => v.SalePrice).HasPrecision(18, 2);
        builder.Property(v => v.Sales).HasPrecision(18, 2);
        builder.Property(v => v.Fees).HasPrecision(18, 2);
        builder.Property(v => v.Refunds).HasPrecision(18, 2);
        builder.Property(v => v.Other).HasPrecision(18, 2);
        builder.Property(v => v.Net).HasPrecision(18, 2);
    }
}
