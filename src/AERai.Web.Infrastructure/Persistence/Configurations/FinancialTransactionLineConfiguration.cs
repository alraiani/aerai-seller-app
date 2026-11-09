using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="FinancialTransactionLine"/> to <c>core.FinancialTransactionLine</c>.</summary>
internal sealed class FinancialTransactionLineConfiguration : IEntityTypeConfiguration<FinancialTransactionLine>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<FinancialTransactionLine> builder)
    {
        builder.ToTable("FinancialTransactionLine", Schemas.Core);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Sku).HasMaxLength(64);
        builder.Property(l => l.Asin).HasMaxLength(16);
        builder.Property(l => l.BreakdownPath).HasMaxLength(400);
        builder.Property(l => l.Amount).HasPrecision(18, 2);
        builder.HasIndex(l => l.FinancialTransactionId).IncludeProperties(l => new { l.Sku, l.Units, l.BreakdownPath, l.Amount });
    }
}
