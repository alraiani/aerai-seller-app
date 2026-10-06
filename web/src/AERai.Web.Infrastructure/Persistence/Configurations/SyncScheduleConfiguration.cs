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

        // Names are unique among active schedules only, so a deleted schedule's name can be reused.
        builder.HasIndex(s => s.Name).IsUnique().HasFilter("[DeletedAt] IS NULL");
        builder.Property(s => s.ReportType).HasConversion<string>().HasMaxLength(32);
        builder.Property(s => s.Frequency).HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.TimeZoneId).HasMaxLength(64);
        builder.Property(s => s.UpdatedBy).HasMaxLength(256);
        builder.Property(s => s.Notes).HasMaxLength(500);
        builder.Property(s => s.OwnerEmail).HasMaxLength(256);
        builder.Property(s => s.DeletedBy).HasMaxLength(256);
        builder.Ignore(s => s.IsDeleted);

        // Soft delete: every query (scheduler, dashboard, pages) sees active schedules only unless it
        // opts in with IgnoreQueryFilters().
        builder.HasQueryFilter(s => s.DeletedAt == null);

        // The scheduler polls "enabled and due" every tick.
        builder.HasIndex(s => new { s.IsEnabled, s.NextRunAt });
    }
}
