using AERai.Web.Domain.Alerts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="StockAlertRead"/> to <c>ops.StockAlertRead</c>.</summary>
internal sealed class StockAlertReadConfiguration : IEntityTypeConfiguration<StockAlertRead>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StockAlertRead> builder)
    {
        builder.ToTable("StockAlertRead", Schemas.Operations);
        builder.HasKey(r => new { r.StockAlertId, r.UserEmail });
        builder.Property(r => r.UserEmail).HasMaxLength(256);
        builder.HasOne<StockAlert>().WithMany().HasForeignKey(r => r.StockAlertId).OnDelete(DeleteBehavior.Cascade);
    }
}
