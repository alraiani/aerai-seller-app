using AERai.Web.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps the keyless <see cref="ProfitLine"/> to <c>rpt.vw_ProfitLine</c>.</summary>
internal sealed class ProfitLineConfiguration : IEntityTypeConfiguration<ProfitLine>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProfitLine> builder)
    {
        builder.ToView("vw_ProfitLine", Schemas.Reporting).HasNoKey();
        builder.Property(v => v.MarketplaceId).HasMaxLength(16);
        builder.Property(v => v.Source).HasMaxLength(10);
        builder.Property(v => v.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.Amount).HasPrecision(18, 2);
    }
}
