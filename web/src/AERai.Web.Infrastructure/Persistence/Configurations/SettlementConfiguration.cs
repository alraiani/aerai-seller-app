using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Settlement"/> to <c>core.Settlement</c>.</summary>
internal sealed class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.ToTable("Settlement", Schemas.Core);
        builder.HasKey(s => s.SettlementId);
        builder.Property(s => s.SettlementId).HasMaxLength(32);
        builder.Property(s => s.Currency).HasMaxLength(3);
        builder.HasMany(s => s.Lines).WithOne(l => l.Settlement).HasForeignKey(l => l.SettlementId).OnDelete(DeleteBehavior.Cascade);
    }
}
