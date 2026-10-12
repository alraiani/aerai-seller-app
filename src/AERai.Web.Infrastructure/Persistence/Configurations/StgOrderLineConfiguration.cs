using AERai.Web.Domain.Staging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="StgOrderLine"/> to <c>stg.OrderLine</c>.</summary>
internal sealed class StgOrderLineConfiguration : StagingRowConfiguration<StgOrderLine>
{
    /// <inheritdoc/>
    protected override string TableName => "OrderLine";
}
