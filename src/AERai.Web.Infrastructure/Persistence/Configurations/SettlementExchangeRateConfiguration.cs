using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="SettlementExchangeRate"/> to <c>core.SettlementExchangeRate</c>.</summary>
internal sealed class SettlementExchangeRateConfiguration : IEntityTypeConfiguration<SettlementExchangeRate>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SettlementExchangeRate> builder)
    {
        builder.ToTable("SettlementExchangeRate", Schemas.Core);
        builder.HasKey(x => x.SettlementId);
        builder.Property(x => x.SettlementId).HasMaxLength(32);
        builder.Property(x => x.FromCurrency).HasMaxLength(3);
        builder.Property(x => x.ToCurrency).HasMaxLength(3);
        builder.Property(x => x.MarketRate).HasPrecision(19, 10);
        builder.Property(x => x.MarketSource).HasMaxLength(32);
        builder.Property(x => x.ManualRate).HasPrecision(19, 10);
        builder.Property(x => x.ManualHomeAmount).HasPrecision(18, 2);
        builder.Property(x => x.ManualBy).HasMaxLength(256);
        builder.Ignore(x => x.EffectiveRate);
    }
}
