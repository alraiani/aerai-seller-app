using AERai.Web.Domain.Staging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="StgSettlementLine"/> to <c>stg.SettlementLine</c>.</summary>
internal sealed class StgSettlementLineConfiguration : StagingRowConfiguration<StgSettlementLine>
{
    /// <inheritdoc/>
    protected override string TableName => "SettlementLine";
}
