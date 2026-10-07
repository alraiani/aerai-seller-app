using AERai.Web.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

// The views themselves are created by migration SQL (see Persistence/Sql); these mappings are
// read-only and keyless, so EF never tries to create, insert into, or track them.

/// <summary>Maps <see cref="DailySalesBySku"/> to <c>rpt.vw_DailySalesBySku</c>.</summary>
internal sealed class DailySalesBySkuConfiguration : IEntityTypeConfiguration<DailySalesBySku>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DailySalesBySku> builder)
    {
        builder.ToView("vw_DailySalesBySku", Schemas.Reporting).HasNoKey();
        builder.Property(v => v.MarketplaceId).HasMaxLength(16);
        builder.Property(v => v.Revenue).HasPrecision(18, 2);
    }
}
