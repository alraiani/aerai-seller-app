using AERai.Web.Domain.Core;
using AERai.Web.Domain.Staging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ImportBatch"/> to <c>stg.ImportBatch</c>.</summary>
internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("ImportBatch", Schemas.Staging);
        builder.HasKey(b => b.Id);
        builder.Property(b => b.MarketplaceId).HasMaxLength(16);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(b => b.MarketplaceId).OnDelete(DeleteBehavior.Restrict);

        // Stored as text because the promotion procedure reads and writes these values by name.
        builder.Property(b => b.Source).HasConversion<string>().HasMaxLength(32);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(32);

        builder.Property(b => b.FileName).HasMaxLength(260);
        builder.Property(b => b.UploadedBy).HasMaxLength(256);
        builder.Property(b => b.ErrorMessage).HasMaxLength(2000);

        // Blob names are limited to 1,024 characters; SHA-256 hex is always 64.
        builder.Property(b => b.RawFilePath).HasMaxLength(1024);
        builder.Property(b => b.RawFileSha256).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(b => b.RawFileSha256);
        builder.HasIndex(b => b.UploadedAt);

        builder.HasMany(b => b.OrderLines).WithOne().HasForeignKey(r => r.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.InventoryRows).WithOne().HasForeignKey(r => r.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.FbaReservedRows).WithOne().HasForeignKey(r => r.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.FbaInventoryRows).WithOne().HasForeignKey(r => r.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.SettlementLines).WithOne().HasForeignKey(r => r.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
    }
}
