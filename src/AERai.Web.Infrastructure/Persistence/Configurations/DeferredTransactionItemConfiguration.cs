using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="DeferredTransactionItem"/> to <c>core.DeferredTransactionItem</c>.</summary>
internal sealed class DeferredTransactionItemConfiguration : IEntityTypeConfiguration<DeferredTransactionItem>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DeferredTransactionItem> builder)
    {
        builder.ToTable("DeferredTransactionItem", Schemas.Core);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.TransactionId).HasMaxLength(128);
        builder.Property(i => i.Sku).HasMaxLength(64);
        builder.Property(i => i.Asin).HasMaxLength(16);
        builder.Property(i => i.Principal).HasPrecision(18, 2);
        builder.Property(i => i.Amount).HasPrecision(18, 2);
        builder.HasIndex(i => i.TransactionId);
    }
}
