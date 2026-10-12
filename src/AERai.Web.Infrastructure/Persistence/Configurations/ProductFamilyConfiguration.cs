using AERai.Web.Domain.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AERai.Web.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ProductFamily"/> to <c>core.ProductFamily</c>.</summary>
internal sealed class ProductFamilyConfiguration : IEntityTypeConfiguration<ProductFamily>
{
    /// <summary>Longest family name accepted.</summary>
    internal const int NameMaxLength = 100;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductFamily> builder)
    {
        builder.ToTable("ProductFamily", Schemas.Core);
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Name).HasMaxLength(NameMaxLength);

        // The database's default collation is case-insensitive, so "Mats" and "mats" cannot both exist.
        builder.HasIndex(f => f.Name).IsUnique();
    }
}
