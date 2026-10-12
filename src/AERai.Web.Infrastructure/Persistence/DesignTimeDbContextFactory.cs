using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AERai.Web.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without the web host or any secrets.
/// </summary>
/// <remarks>
/// Creating a migration never opens a connection, so a placeholder connection string is sufficient.
/// Commands that do connect (<c>database update</c>) read <c>AERAI_SQL</c> when it is set.
/// </remarks>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <inheritdoc/>
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AERAI_SQL")
            ?? "Server=localhost,1433;Database=AERaiSeller;Integrated Security=False;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, DependencyInjection.ConfigureSqlServer)
            .Options;

        return new AppDbContext(options);
    }
}
