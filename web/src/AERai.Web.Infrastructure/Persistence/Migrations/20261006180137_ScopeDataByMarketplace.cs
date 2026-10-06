using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Scopes data to marketplaces: adds MarketplaceId to imports, orders, inventory snapshots,
    /// settlements, sync schedules, and sync runs (existing rows are US, the only marketplace pulled
    /// before), moves cost of goods into per-marketplace core.ProductCost, keeps the orders report's
    /// sales-channel in staging, and makes the promotion procedure and reporting views marketplace-aware.
    /// </summary>
    public partial class ScopeDataByMarketplace : Migration
    {
        private const string UnitedStates = "ATVPDKIKX0DER";

        private static readonly (string Schema, string Table)[] ScopedTables =
        [
            ("stg", "ImportBatch"),
            ("core", "Order"),
            ("core", "InventorySnapshot"),
            ("core", "Settlement"),
            ("ops", "SyncSchedule"),
            ("ops", "SyncRun"),
        ];

        private static readonly string[] Views =
            ["rpt.vw_SalesLine", "rpt.vw_DailySalesBySku", "rpt.vw_InventoryPosition", "rpt.vw_OrderSummary", "rpt.vw_SettlementSummary"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old unique key (Sku, SnapshotDate, State) would collide once two marketplaces stock the same SKU.
            migrationBuilder.DropIndex(name: "IX_InventorySnapshot_Sku_SnapshotDate_State", schema: "core", table: "InventorySnapshot");

            foreach (var (schema, table) in ScopedTables)
            {
                // Added with a US default so existing rows are backfilled, then the default is dropped:
                // new rows must always say which marketplace they belong to.
                migrationBuilder.AddColumn<string>(
                    name: "MarketplaceId", schema: schema, table: table, type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: UnitedStates);
                migrationBuilder.AlterColumn<string>(
                    name: "MarketplaceId", schema: schema, table: table, type: "nvarchar(16)", maxLength: 16, nullable: false,
                    oldClrType: typeof(string), oldType: "nvarchar(16)", oldMaxLength: 16, oldDefaultValue: UnitedStates);
                migrationBuilder.CreateIndex(name: $"IX_{table}_MarketplaceId", schema: schema, table: table, column: "MarketplaceId");
                migrationBuilder.AddForeignKey(
                    name: $"FK_{table}_Marketplace_MarketplaceId", schema: schema, table: table, column: "MarketplaceId",
                    principalSchema: "core", principalTable: "Marketplace", principalColumn: "MarketplaceId", onDelete: ReferentialAction.Restrict);
            }

            migrationBuilder.CreateIndex(
                name: "IX_InventorySnapshot_MarketplaceId_Sku_SnapshotDate_State", schema: "core", table: "InventorySnapshot",
                columns: ["MarketplaceId", "Sku", "SnapshotDate", "State"], unique: true);
            migrationBuilder.CreateIndex(name: "IX_InventorySnapshot_Sku", schema: "core", table: "InventorySnapshot", column: "Sku");

            migrationBuilder.AddColumn<string>(
                name: "SalesChannel", schema: "stg", table: "OrderLine", type: "nvarchar(400)", maxLength: 400, nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductCost",
                schema: "core",
                columns: table => new
                {
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CostOfGoods = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCost", x => new { x.Sku, x.MarketplaceId });
                    table.CheckConstraint("CK_ProductCost_NonNegative", "[CostOfGoods] >= 0");
                    table.ForeignKey(
                        name: "FK_ProductCost_Marketplace_MarketplaceId", column: x => x.MarketplaceId,
                        principalSchema: "core", principalTable: "Marketplace", principalColumn: "MarketplaceId", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductCost_Product_Sku", column: x => x.Sku,
                        principalSchema: "core", principalTable: "Product", principalColumn: "Sku", onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(name: "IX_ProductCost_MarketplaceId", schema: "core", table: "ProductCost", column: "MarketplaceId");

            // Costs entered so far were for US sales (the only marketplace synced until now).
            migrationBuilder.Sql($"""
                INSERT core.ProductCost (Sku, MarketplaceId, CostOfGoods, UpdatedAt)
                SELECT Sku, N'{UnitedStates}', CostOfGoods, UpdatedAt
                FROM core.Product
                WHERE CostOfGoods IS NOT NULL;
                """);
            migrationBuilder.DropColumn(name: "CostOfGoods", schema: "core", table: "Product");

            migrationBuilder.Sql(SqlResource.Read("V005_Marketplaces", "core.usp_PromoteImportBatch.sql"));
            foreach (var view in Views)
            {
                migrationBuilder.Sql(SqlResource.Read("V005_Marketplaces", $"{view}.sql"));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the previous procedure and views first so nothing references the dropped columns.
            migrationBuilder.Sql(SqlResource.Read("V002_SpApiIngestion", "core.usp_PromoteImportBatch.sql"));
            migrationBuilder.Sql(SqlResource.Read("V004_SalesLineView", "rpt.vw_SalesLine.sql"));
            foreach (var view in Views[1..])
            {
                migrationBuilder.Sql(SqlResource.Read("V001_Initial", $"{view}.sql"));
            }

            migrationBuilder.AddColumn<decimal>(
                name: "CostOfGoods", schema: "core", table: "Product", type: "decimal(18,2)", precision: 18, scale: 2, nullable: true);
            migrationBuilder.Sql($"""
                UPDATE p SET CostOfGoods = c.CostOfGoods
                FROM core.Product AS p
                INNER JOIN core.ProductCost AS c ON c.Sku = p.Sku AND c.MarketplaceId = N'{UnitedStates}';
                """);
            migrationBuilder.DropTable(name: "ProductCost", schema: "core");

            migrationBuilder.DropColumn(name: "SalesChannel", schema: "stg", table: "OrderLine");

            // The pre-marketplace unique key cannot hold two marketplaces' snapshots of a SKU, so only
            // US inventory survives a downgrade (other marketplaces can be re-pulled after upgrading again).
            migrationBuilder.DropIndex(name: "IX_InventorySnapshot_MarketplaceId_Sku_SnapshotDate_State", schema: "core", table: "InventorySnapshot");
            migrationBuilder.DropIndex(name: "IX_InventorySnapshot_Sku", schema: "core", table: "InventorySnapshot");
            migrationBuilder.Sql($"DELETE FROM core.InventorySnapshot WHERE MarketplaceId <> N'{UnitedStates}';");

            foreach (var (schema, table) in ScopedTables)
            {
                migrationBuilder.DropForeignKey(name: $"FK_{table}_Marketplace_MarketplaceId", schema: schema, table: table);
                migrationBuilder.DropIndex(name: $"IX_{table}_MarketplaceId", schema: schema, table: table);
                migrationBuilder.DropColumn(name: "MarketplaceId", schema: schema, table: table);
            }

            migrationBuilder.CreateIndex(
                name: "IX_InventorySnapshot_Sku_SnapshotDate_State", schema: "core", table: "InventorySnapshot",
                columns: ["Sku", "SnapshotDate", "State"], unique: true);
        }
    }
}
