using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="DeferredTransaction"/> to <c>core.DeferredTransaction</c>.</summary>
internal sealed class DeferredTransactionConfiguration : IEntityTypeConfiguration<DeferredTransaction>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DeferredTransaction> builder)
    {
        builder.ToTable("DeferredTransaction", Schemas.Core);
        builder.HasKey(t => t.TransactionId);
        builder.Property(t => t.TransactionId).HasMaxLength(128);
        builder.Property(t => t.MarketplaceId).HasMaxLength(16);
        builder.Property(t => t.AmazonOrderId).HasMaxLength(64);
        builder.Property(t => t.TransactionType).HasMaxLength(64);
        builder.Property(t => t.Description).HasMaxLength(256);
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.Currency).HasMaxLength(3);
        builder.Property(t => t.DeferralReason).HasMaxLength(32);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(t => t.SettlementId).HasMaxLength(32);
        builder.HasMany(t => t.Items).WithOne().HasForeignKey(i => i.TransactionId).OnDelete(DeleteBehavior.Cascade);

        // The tile and the sync read a marketplace's held transactions; the settlement and order markers look up by order.
        builder.HasIndex(t => new { t.MarketplaceId, t.Status });
        builder.HasIndex(t => t.AmazonOrderId);

        // The settlement pages add up what each settlement's sales had held back.
        builder.HasIndex(t => t.SettlementId);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(t => t.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
