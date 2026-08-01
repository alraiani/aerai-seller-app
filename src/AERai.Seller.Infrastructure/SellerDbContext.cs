using AERai.Seller.Domain;
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

        modelBuilder.Entity<SyncMetadata>(entity =>
        {
            entity.HasKey(s => s.SyncJobName);
        });
    }
}
