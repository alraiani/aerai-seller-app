using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Marketplace"/> to <c>core.Marketplace</c> and seeds the known marketplaces.</summary>
internal sealed class MarketplaceConfiguration : IEntityTypeConfiguration<Marketplace>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Marketplace> builder)
    {
        builder.ToTable("Marketplace", Schemas.Core);
        builder.HasKey(m => m.MarketplaceId);
        builder.Property(m => m.MarketplaceId).HasMaxLength(16);
        builder.Property(m => m.Code).HasMaxLength(8);
        builder.HasIndex(m => m.Code).IsUnique();
        builder.Property(m => m.Name).HasMaxLength(64);
        builder.Property(m => m.Region).HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(m => m.TimeZoneId).HasMaxLength(64);
        builder.Property(m => m.SalesChannel).HasMaxLength(32);

        // The UK starts inactive: it is in the EU region, which needs its own SP-API authorization.
        builder.HasData(
            new Marketplace
            {
                MarketplaceId = MarketplaceIds.UnitedStates, Code = "US", Name = "United States", Region = AmazonRegion.NorthAmerica,
                Currency = "USD", TimeZoneId = "America/New_York", SalesChannel = "Amazon.com", SortOrder = 1, IsActive = true,
            },
            new Marketplace
            {
                MarketplaceId = MarketplaceIds.Canada, Code = "CA", Name = "Canada", Region = AmazonRegion.NorthAmerica,
                Currency = "CAD", TimeZoneId = "America/Toronto", SalesChannel = "Amazon.ca", SortOrder = 2, IsActive = true,
            },
            new Marketplace
            {
                MarketplaceId = MarketplaceIds.UnitedKingdom, Code = "UK", Name = "United Kingdom", Region = AmazonRegion.Europe,
                Currency = "GBP", TimeZoneId = "Europe/London", SalesChannel = "Amazon.co.uk", SortOrder = 3, IsActive = false,
            });
    }
}
