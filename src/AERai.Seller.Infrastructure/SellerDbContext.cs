using AERai.Seller.Domain;
using AERai.Seller.Domain.Ai;
using AERai.Seller.Domain.Staging;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure;

public class SellerDbContext(DbContextOptions<SellerDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventorySnapshot> InventorySnapshots => Set<InventorySnapshot>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<FinancialEvent> FinancialEvents => Set<FinancialEvent>();
    public DbSet<SettlementReport> SettlementReports => Set<SettlementReport>();
    public DbSet<SyncMetadata> SyncMetadata => Set<SyncMetadata>();
    public DbSet<CatalogParent> CatalogParents => Set<CatalogParent>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<AwdInventorySnapshot> AwdInventorySnapshots => Set<AwdInventorySnapshot>();
    public DbSet<SettlementLineItem> SettlementLineItems => Set<SettlementLineItem>();
    public DbSet<BookkeepingAccountMapping> BookkeepingAccountMappings => Set<BookkeepingAccountMapping>();
    public DbSet<BookkeepingSettings> BookkeepingSettings => Set<BookkeepingSettings>();
    public DbSet<BookkeepingExportRecord> BookkeepingExportRecords => Set<BookkeepingExportRecord>();

    // AI schema (AERai.Seller.Domain.Ai) — demand forecasting & replenishment inputs/outputs,
    // kept logically separate from the staged import data above.
    public DbSet<LeadTimeProfile> LeadTimeProfiles => Set<LeadTimeProfile>();
    public DbSet<DemandForecast> DemandForecasts => Set<DemandForecast>();
    public DbSet<ReplenishmentRecommendation> ReplenishmentRecommendations => Set<ReplenishmentRecommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Sku);
        });

        modelBuilder.Entity<InventorySnapshot>(entity =>
        {
            entity.HasKey(s => s.Id);
            // Idempotent upsert key: one reading per SKU/state/date.
            entity.HasIndex(s => new { s.Sku, s.State, s.SnapshotDate }).IsUnique();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.AmazonOrderId);
            entity.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey(i => i.AmazonOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(i => i.Id);
        });

        modelBuilder.Entity<FinancialEvent>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => new { f.AmazonOrderId, f.EventSubType, f.PostedDate });
        });

        modelBuilder.Entity<SettlementReport>(entity =>
        {
            entity.HasKey(s => s.SettlementId);
        });

        modelBuilder.Entity<SettlementLineItem>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.HasIndex(l => l.SettlementId);
        });

        modelBuilder.Entity<BookkeepingAccountMapping>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.AmountType, m.AmountDescription }).IsUnique();
        });

        modelBuilder.Entity<BookkeepingSettings>(entity =>
        {
            entity.HasKey(s => s.Id);
        });

        modelBuilder.Entity<BookkeepingExportRecord>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.SettlementId);
        });

        modelBuilder.Entity<SyncMetadata>(entity =>
        {
            entity.HasKey(s => s.SyncJobName);
        });

        modelBuilder.Entity<CatalogParent>(entity =>
        {
            entity.HasKey(p => p.ParentAsin);
            entity.HasMany(p => p.Items)
                .WithOne()
                .HasForeignKey(i => i.ParentAsin)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CatalogItem>(entity =>
        {
            entity.HasKey(i => i.Asin);
        });

        modelBuilder.Entity<AwdInventorySnapshot>(entity =>
        {
            entity.HasKey(s => s.Id);
            // Idempotent upsert key: one reading per SKU/date (no per-state dimension, unlike FBA).
            entity.HasIndex(s => new { s.Sku, s.SnapshotDate }).IsUnique();
        });

        modelBuilder.Entity<LeadTimeProfile>(entity =>
        {
            entity.HasKey(p => p.Sku);
        });

        modelBuilder.Entity<DemandForecast>(entity =>
        {
            entity.HasKey(f => f.Id);
            // Fast lookup of the latest forecast per SKU, mirroring InventorySnapshot's pattern.
            entity.HasIndex(f => new { f.Sku, f.ComputedAt });
        });

        modelBuilder.Entity<ReplenishmentRecommendation>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => new { r.Sku, r.ComputedAt });
        });
    }
}
