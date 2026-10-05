using AERai.Web.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="SalesLine"/> to <c>rpt.vw_SalesLine</c> (read-only, keyless).</summary>
internal sealed class SalesLineConfiguration : IEntityTypeConfiguration<SalesLine>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SalesLine> builder)
    {
        builder.ToView("vw_SalesLine", Schemas.Reporting).HasNoKey();
        builder.Property(v => v.ItemPrice).HasPrecision(18, 2);
    }
}
