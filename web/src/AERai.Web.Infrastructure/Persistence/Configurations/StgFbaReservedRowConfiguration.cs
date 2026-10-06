using AERai.Web.Domain.Staging;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="StgFbaReservedRow"/> to <c>stg.FbaReservedRow</c>.</summary>
internal sealed class StgFbaReservedRowConfiguration : StagingRowConfiguration<StgFbaReservedRow>
{
    /// <inheritdoc/>
    protected override string TableName => "FbaReservedRow";
}
