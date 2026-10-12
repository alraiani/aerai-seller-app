using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="BookkeepingAccountMapping"/> to <c>core.BookkeepingAccountMapping</c>.</summary>
internal sealed class BookkeepingAccountMappingConfiguration : IEntityTypeConfiguration<BookkeepingAccountMapping>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BookkeepingAccountMapping> builder)
    {
        builder.ToTable("BookkeepingAccountMapping", Schemas.Core);
        builder.HasKey(m => m.Id);

        // Same lengths as core.SettlementLine.AmountType / AmountDescription, whose values these key.
        builder.Property(m => m.AmountType).HasMaxLength(64);
        builder.Property(m => m.AmountDescription).HasMaxLength(128);
        builder.Property(m => m.QuickBooksAccountName).HasMaxLength(200);
        builder.Property(m => m.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(m => new { m.AmountType, m.AmountDescription }).IsUnique();
    }
}
