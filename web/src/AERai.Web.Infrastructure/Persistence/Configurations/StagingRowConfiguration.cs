using AERai.Web.Domain.Staging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared mapping for every <see cref="StagingRow"/> table: one row per file line, keyed for
/// per-batch access, with every business column as bounded <c>nvarchar</c>.
/// </summary>
/// <typeparam name="TRow">The concrete staging row type.</typeparam>
internal abstract class StagingRowConfiguration<TRow> : IEntityTypeConfiguration<TRow>
    where TRow : StagingRow
{
    /// <summary>Table name inside the <c>stg</c> schema.</summary>
    protected abstract string TableName { get; }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TRow> builder)
    {
        builder.ToTable(TableName, Schemas.Staging);
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.ImportBatchId, r.RowNumber }).IsUnique();
        builder.Property(r => r.RawLine).HasColumnType("nvarchar(max)");
        builder.Property(r => r.ErrorMessage).HasMaxLength(1000);

        // Staging is deliberately untyped: every string column gets the same bound so any value
        // the mapper accepts fits, and conversion errors surface during promotion instead of on insert.
        foreach (var property in builder.Metadata.GetProperties().Where(p => p.ClrType == typeof(string)))
        {
            if (property.Name is not (nameof(StagingRow.RawLine) or nameof(StagingRow.ErrorMessage)))
            {
                property.SetMaxLength(StagingRow.MaxFieldLength);
            }
        }
    }
}
