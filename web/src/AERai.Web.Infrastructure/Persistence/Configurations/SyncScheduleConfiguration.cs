using AERai.Web.Domain.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="SyncSchedule"/> to <c>ops.SyncSchedule</c>.</summary>
internal sealed class SyncScheduleConfiguration : IEntityTypeConfiguration<SyncSchedule>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SyncSchedule> builder)
    {
        builder.ToTable("SyncSchedule", Schemas.Operations);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(100);
        builder.HasIndex(s => s.Name).IsUnique();
        builder.Property(s => s.ReportType).HasConversion<string>().HasMaxLength(32);
        builder.Property(s => s.Frequency).HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.TimeZoneId).HasMaxLength(64);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);

        // The scheduler polls "enabled and due" every tick.
        builder.HasIndex(s => new { s.IsEnabled, s.NextRunAt });
    }
}
