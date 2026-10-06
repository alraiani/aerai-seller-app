using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="LeadTimeProfile"/> to <c>core.LeadTimeProfile</c>.</summary>
internal sealed class LeadTimeProfileConfiguration : IEntityTypeConfiguration<LeadTimeProfile>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LeadTimeProfile> builder)
    {
        builder.ToTable("LeadTimeProfile", Schemas.Core, t =>
        {
            t.HasCheckConstraint("CK_LeadTimeProfile_SupplierLeadTimeDays", "[SupplierLeadTimeDays] BETWEEN 0 AND 730");
            t.HasCheckConstraint("CK_LeadTimeProfile_PrepTimeDays", "[PrepTimeDays] BETWEEN 0 AND 730");
            t.HasCheckConstraint("CK_LeadTimeProfile_TransitDays", "[TransitDays] BETWEEN 0 AND 730");
            t.HasCheckConstraint("CK_LeadTimeProfile_SafetyStockDays", "[SafetyStockDays] BETWEEN 0 AND 730");
            t.HasCheckConstraint("CK_LeadTimeProfile_TargetStockDays", "[TargetStockDays] BETWEEN 1 AND 730");
        });
        builder.HasKey(p => new { p.Sku, p.MarketplaceId });
        builder.Property(p => p.Sku).HasMaxLength(64);
        builder.Property(p => p.MarketplaceId).HasMaxLength(16);
        builder.Property(p => p.UpdatedBy).HasMaxLength(256);
        builder.HasOne<Product>().WithMany().HasForeignKey(p => p.Sku).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Marketplace>().WithMany().HasForeignKey(p => p.MarketplaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
