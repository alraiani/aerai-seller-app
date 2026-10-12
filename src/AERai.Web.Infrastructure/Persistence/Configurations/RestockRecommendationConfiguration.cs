using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="RestockRecommendation"/> to <c>core.RestockRecommendation</c>.</summary>
internal sealed class RestockRecommendationConfiguration : IEntityTypeConfiguration<RestockRecommendation>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RestockRecommendation> builder)
    {
        builder.ToTable("RestockRecommendation", Schemas.Core, t =>
            t.HasCheckConstraint("CK_RestockRecommendation_Quantity", "[RecommendedQuantity] >= 0"));
        builder.HasKey(r => new { r.MarketplaceId, r.Sku });
        builder.Property(r => r.MarketplaceId).HasMaxLength(16);
        builder.Property(r => r.Sku).HasMaxLength(64);
        builder.Property(r => r.RecommendedAction).HasMaxLength(100);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(r => r.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(r => r.Sku).OnDelete(DeleteBehavior.Cascade);
    }
}
