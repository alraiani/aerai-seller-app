using AERai.Web.Domain.Ingestion;
using AERai.Web.Domain.Staging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="IngestedReport"/> to <c>ops.IngestedReport</c>.</summary>
internal sealed class IngestedReportConfiguration : IEntityTypeConfiguration<IngestedReport>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IngestedReport> builder)
    {
        builder.ToTable("IngestedReport", Schemas.Operations);

        // The primary key on the Amazon report id is what makes duplicate ingestion impossible.
        builder.HasKey(r => r.AmazonReportId);
        builder.Property(r => r.AmazonReportId).HasMaxLength(64);
        builder.Property(r => r.ReportType).HasConversion<string>().HasMaxLength(32);
        builder.HasOne<ImportBatch>().WithMany().HasForeignKey(r => r.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SyncRun>().WithMany().HasForeignKey(r => r.SyncRunId).OnDelete(DeleteBehavior.Restrict);
    }
}
