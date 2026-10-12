using AERai.Web.Domain.Staging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="StgInventoryRow"/> to <c>stg.InventoryRow</c>.</summary>
internal sealed class StgInventoryRowConfiguration : StagingRowConfiguration<StgInventoryRow>
{
    /// <inheritdoc/>
    protected override string TableName => "InventoryRow";
}
