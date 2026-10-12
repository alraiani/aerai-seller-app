using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="BookkeepingSettings"/> to <c>core.BookkeepingSettings</c>.</summary>
internal sealed class BookkeepingSettingsConfiguration : IEntityTypeConfiguration<BookkeepingSettings>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BookkeepingSettings> builder)
    {
        builder.ToTable("BookkeepingSettings", Schemas.Core);
        builder.HasKey(s => s.MarketplaceId);
        builder.Property(s => s.MarketplaceId).HasMaxLength(16);
        builder.Property(s => s.DepositAccountName).HasMaxLength(200);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(s => s.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
