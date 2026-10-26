using AERai.Web.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="SettlementLineDetail"/> to <c>rpt.vw_SettlementLine</c> (read-only, keyless).</summary>
internal sealed class SettlementLineDetailConfiguration : IEntityTypeConfiguration<SettlementLineDetail>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SettlementLineDetail> builder)
    {
        builder.ToView("vw_SettlementLine", Schemas.Reporting).HasNoKey();
        builder.Property(v => v.MarketplaceId).HasMaxLength(16);
        builder.Property(v => v.HomeCurrency).HasMaxLength(3);
        builder.Property(v => v.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.ExchangeRate).HasPrecision(19, 10);
        builder.Property(v => v.HomeAmount).HasPrecision(18, 2);
        builder.Property(v => v.HomeSales).HasPrecision(18, 2);
        builder.Property(v => v.HomeExpenses).HasPrecision(18, 2);
        builder.Property(v => v.HomeRefunds).HasPrecision(18, 2);
        builder.Property(v => v.HomeReserve).HasPrecision(18, 2);
        builder.Property(v => v.SalePrice).HasPrecision(18, 2);
        builder.Property(v => v.Amount).HasPrecision(18, 2);
        builder.Property(v => v.Sales).HasPrecision(18, 2);
        builder.Property(v => v.Expenses).HasPrecision(18, 2);
        builder.Property(v => v.Refunds).HasPrecision(18, 2);
        builder.Property(v => v.Reserve).HasPrecision(18, 2);
    }
}
