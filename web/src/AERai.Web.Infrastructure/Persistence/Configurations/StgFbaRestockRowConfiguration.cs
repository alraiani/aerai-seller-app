using AERai.Web.Domain.Staging;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="StgFbaRestockRow"/> to <c>stg.FbaRestockRow</c>.</summary>
internal sealed class StgFbaRestockRowConfiguration : StagingRowConfiguration<StgFbaRestockRow>
{
    /// <inheritdoc/>
    protected override string TableName => "FbaRestockRow";
}
