using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="SyncRun"/> to <c>ops.SyncRun</c>.</summary>
internal sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.ToTable("SyncRun", Schemas.Operations);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ReportType).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.MarketplaceId).HasMaxLength(16);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(r => r.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(r => r.Trigger).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.TriggeredBy).HasMaxLength(256);
        builder.Property(r => r.AmazonReportIds).HasMaxLength(1000);
        builder.Property(r => r.ImportBatchIds).HasMaxLength(1000);
        builder.Property(r => r.Message).HasMaxLength(4000);
        builder.HasIndex(r => new { r.SyncScheduleId, r.StartedAt });
        builder.Property(r => r.PendingReportId).HasMaxLength(64);

        // The scheduler looks up waiting runs on every wake; only a handful ever match.
        builder.HasIndex(r => r.NextCheckAt).HasFilter("[PendingReportId] IS NOT NULL");
        builder.HasOne<SyncSchedule>().WithMany().HasForeignKey(r => r.SyncScheduleId).OnDelete(DeleteBehavior.Cascade);
    }
}
