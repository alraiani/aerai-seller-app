using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Domain.Reporting;
using AERai.Web.Domain.Staging;
using AERai.Web.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the centralized AERai database: Identity, staging, core, and reporting views.
/// </summary>
/// <param name="options">Context options configured in <see cref="DependencyInjection"/>.</param>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    /// <summary>Staging batches.</summary>
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    /// <summary>Raw order lines.</summary>
    public DbSet<StgOrderLine> StgOrderLines => Set<StgOrderLine>();

    /// <summary>Raw inventory rows.</summary>
    public DbSet<StgInventoryRow> StgInventoryRows => Set<StgInventoryRow>();

    /// <summary>Raw FBA inventory rows.</summary>
    public DbSet<StgFbaInventoryRow> StgFbaInventoryRows => Set<StgFbaInventoryRow>();

    /// <summary>Raw settlement lines.</summary>
    public DbSet<StgSettlementLine> StgSettlementLines => Set<StgSettlementLine>();

    /// <summary>Marketplaces the business sells in (<c>core.Marketplace</c>).</summary>
    public DbSet<Marketplace> Marketplaces => Set<Marketplace>();

    /// <summary>Curated products.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>Per-marketplace product costs.</summary>
    public DbSet<ProductCost> ProductCosts => Set<ProductCost>();

    /// <summary>Curated orders.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Curated order items.</summary>
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    /// <summary>Curated inventory snapshots.</summary>
    public DbSet<InventorySnapshot> InventorySnapshots => Set<InventorySnapshot>();

    /// <summary>Curated settlements.</summary>
    public DbSet<Settlement> Settlements => Set<Settlement>();

    /// <summary>Curated settlement lines.</summary>
    public DbSet<SettlementLine> SettlementLines => Set<SettlementLine>();

    /// <summary>SP-API ingestion schedules.</summary>
    public DbSet<SyncSchedule> SyncSchedules => Set<SyncSchedule>();

    /// <summary>Ingestion run history.</summary>
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    /// <summary>App-wide sync settings (single row).</summary>
    public DbSet<SyncSettings> SyncSettings => Set<SyncSettings>();

    /// <summary>Ledger of Amazon reports already ingested.</summary>
    public DbSet<IngestedReport> IngestedReports => Set<IngestedReport>();

    /// <summary><c>rpt.vw_SalesLine</c>.</summary>
    public DbSet<SalesLine> SalesLines => Set<SalesLine>();

    /// <summary><c>rpt.vw_DailySalesBySku</c>.</summary>
    public DbSet<DailySalesBySku> DailySalesBySku => Set<DailySalesBySku>();

    /// <summary><c>rpt.vw_OrderSummary</c>.</summary>
    public DbSet<OrderSummary> OrderSummaries => Set<OrderSummary>();

    /// <summary><c>rpt.vw_InventoryPosition</c>.</summary>
    public DbSet<InventoryPosition> InventoryPositions => Set<InventoryPosition>();

    /// <summary><c>rpt.vw_SettlementSummary</c>.</summary>
    public DbSet<SettlementSummary> SettlementSummaries => Set<SettlementSummary>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's own mapping must run first; the calls below only move its tables into the auth schema.
        base.OnModelCreating(builder);

        builder.Entity<AppUser>().ToTable("Users", Schemas.Auth);
        builder.Entity<IdentityRole>().ToTable("Roles", Schemas.Auth);
        builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles", Schemas.Auth);
        builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims", Schemas.Auth);
        builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins", Schemas.Auth);
        builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims", Schemas.Auth);
        builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens", Schemas.Auth);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
