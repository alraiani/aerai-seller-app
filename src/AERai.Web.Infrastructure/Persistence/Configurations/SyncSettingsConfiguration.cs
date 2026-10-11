using AERai.Web.Domain.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="SyncSettings"/> to the single-row <c>ops.SyncSettings</c> table.</summary>
internal sealed class SyncSettingsConfiguration : IEntityTypeConfiguration<SyncSettings>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SyncSettings> builder)
    {
        builder.ToTable("SyncSettings", Schemas.Operations, table =>
            table.HasCheckConstraint("CK_SyncSettings_Singleton", $"[Id] = {SyncSettings.SingletonId}"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.PausedBy).HasMaxLength(256);

        // Seeded so the scheduler can always read the row without an existence check.
        builder.HasData(new SyncSettings { Id = SyncSettings.SingletonId });
    }
}
