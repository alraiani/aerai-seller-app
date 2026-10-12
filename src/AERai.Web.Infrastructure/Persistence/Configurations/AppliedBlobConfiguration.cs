using AERai.Web.Domain.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="AppliedBlob"/> to <c>ops.AppliedBlob</c>.</summary>
internal sealed class AppliedBlobConfiguration : IEntityTypeConfiguration<AppliedBlob>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppliedBlob> builder)
    {
        builder.ToTable("AppliedBlob", Schemas.Operations);
        builder.HasKey(b => b.Path);
        builder.Property(b => b.Path).HasMaxLength(400);
        builder.Property(b => b.Source).HasMaxLength(64);
        builder.Property(b => b.Outcome).HasMaxLength(200);
        builder.Property(b => b.AppliedBy).HasMaxLength(256);
    }
}
