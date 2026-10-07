using AERai.Web.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

// The views themselves are created by migration SQL (see Persistence/Sql); these mappings are
// read-only and keyless, so EF never tries to create, insert into, or track them.

/// <summary>Maps <see cref="InventoryPosition"/> to <c>rpt.vw_InventoryPosition</c>.</summary>
internal sealed class InventoryPositionConfiguration : IEntityTypeConfiguration<InventoryPosition>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InventoryPosition> builder)
    {
        builder.ToView("vw_InventoryPosition", Schemas.Reporting).HasNoKey();
        builder.Property(v => v.MarketplaceId).HasMaxLength(16);
        builder.Property(v => v.Color).HasConversion<string>().HasMaxLength(20);
    }
}
