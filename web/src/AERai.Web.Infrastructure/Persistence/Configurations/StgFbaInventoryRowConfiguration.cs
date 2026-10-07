using AERai.Web.Domain.Staging;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="StgFbaInventoryRow"/> to <c>stg.FbaInventoryRow</c>.</summary>
internal sealed class StgFbaInventoryRowConfiguration : StagingRowConfiguration<StgFbaInventoryRow>
{
    /// <inheritdoc/>
    protected override string TableName => "FbaInventoryRow";
}
