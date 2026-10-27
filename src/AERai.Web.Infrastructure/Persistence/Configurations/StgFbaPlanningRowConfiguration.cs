using AERai.Web.Domain.Staging;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="StgFbaPlanningRow"/> to <c>stg.FbaPlanningRow</c>.</summary>
internal sealed class StgFbaPlanningRowConfiguration : StagingRowConfiguration<StgFbaPlanningRow>
{
    /// <inheritdoc/>
    protected override string TableName => "FbaPlanningRow";
}
