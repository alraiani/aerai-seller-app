using AERai.Web.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

// The views themselves are created by migration SQL (see Persistence/Sql); these mappings are
// read-only and keyless, so EF never tries to create, insert into, or track them.

/// <summary>Maps <see cref="OrderSummary"/> to <c>rpt.vw_OrderSummary</c>.</summary>
internal sealed class OrderSummaryConfiguration : IEntityTypeConfiguration<OrderSummary>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OrderSummary> builder)
    {
        builder.ToView("vw_OrderSummary", Schemas.Reporting).HasNoKey();
        builder.Property(v => v.OrderTotal).HasPrecision(18, 2);
    }
}
