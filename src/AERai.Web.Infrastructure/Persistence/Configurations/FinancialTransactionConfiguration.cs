using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="FinancialTransaction"/> to <c>core.FinancialTransaction</c>.</summary>
internal sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        builder.ToTable("FinancialTransaction", Schemas.Core);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.MarketplaceId).HasMaxLength(16);
        builder.Property(t => t.TransactionId).HasMaxLength(128);
        builder.Property(t => t.TransactionType).HasMaxLength(64);
        builder.Property(t => t.Description).HasMaxLength(256);
        builder.Property(t => t.Status).HasMaxLength(32);
        builder.Property(t => t.AmazonOrderId).HasMaxLength(64);
        builder.Property(t => t.SettlementId).HasMaxLength(32);
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.Currency).HasMaxLength(3);
        builder.HasMany(t => t.Lines).WithOne().HasForeignKey(l => l.FinancialTransactionId).OnDelete(DeleteBehavior.Cascade);

        // The sync upserts by Amazon's id; profit reads a marketplace by posted date; reconciliation by settlement.
        builder.HasIndex(t => new { t.MarketplaceId, t.TransactionId }).IsUnique();
        builder.HasIndex(t => new { t.MarketplaceId, t.PostedDate });
        builder.HasIndex(t => t.SettlementId);
        builder.HasIndex(t => t.AmazonOrderId);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(t => t.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
