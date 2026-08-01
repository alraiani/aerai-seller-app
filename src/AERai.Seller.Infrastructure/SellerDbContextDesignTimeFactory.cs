using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AERai.Seller.Infrastructure;

/// <summary>Lets `dotnet ef migrations` run against this class library without needing the Wpf startup project.</summary>
public sealed class SellerDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SellerDbContext>
{
    public SellerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SellerDbContext>();
        optionsBuilder.UseSqlite($"Data Source={ServiceCollectionExtensions.GetDefaultSqliteDbPath()}");
        return new SellerDbContext(optionsBuilder.Options);
    }
}
