using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="BookkeepingExport"/> to <c>core.BookkeepingExport</c>.</summary>
internal sealed class BookkeepingExportConfiguration : IEntityTypeConfiguration<BookkeepingExport>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BookkeepingExport> builder)
    {
        builder.ToTable("BookkeepingExport", Schemas.Core);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MarketplaceId).HasMaxLength(16);
        builder.Property(e => e.SettlementId).HasMaxLength(32);
        builder.Property(e => e.FileName).HasMaxLength(260);
        builder.Property(e => e.DepositAccountName).HasMaxLength(200);
        builder.Property(e => e.NetTotal).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3);
        builder.Property(e => e.GeneratedBy).HasMaxLength(256);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(e => e.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Settlement>().WithMany().HasForeignKey(e => e.SettlementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.MarketplaceId, e.GeneratedAt });
        builder.HasIndex(e => e.SettlementId);
    }
}
