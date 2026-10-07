using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Supports the inventory worksheet: Amazon's restock recommendations (stg.FbaRestockRow,
    /// core.RestockRecommendation, and a promotion branch for them), a home-stock ledger
    /// (core.HomeStockMovement, opened with one OpeningBalance entry per existing home-stock row so
    /// the ledger adds up to the current balances), and a per-SKU color (Product.Color). The inventory
    /// view gains the color and Amazon's recommendation.
    /// </summary>
    public partial class AddInventoryWorksheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                schema: "core",
                table: "Product",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FbaRestockRow",
                schema: "stg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sku = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Asin = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RecommendedQuantity = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RecommendedShipDate = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RecommendedAction = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    RawLine = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FbaRestockRow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FbaRestockRow_ImportBatch_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "stg",
                        principalTable: "ImportBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HomeStockMovement",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Units = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReversesId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeStockMovement", x => x.Id);
                    table.CheckConstraint("CK_HomeStockMovement_BalanceAfter", "[BalanceAfter] >= 0");
                    table.CheckConstraint("CK_HomeStockMovement_Units", "[Units] <> 0");
                    table.ForeignKey(
                        name: "FK_HomeStockMovement_HomeStockMovement_ReversesId",
                        column: x => x.ReversesId,
                        principalSchema: "core",
                        principalTable: "HomeStockMovement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HomeStockMovement_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HomeStockMovement_Product_Sku",
                        column: x => x.Sku,
                        principalSchema: "core",
                        principalTable: "Product",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RestockRecommendation",
                schema: "core",
                columns: table => new
                {
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecommendedQuantity = table.Column<int>(type: "int", nullable: false),
                    RecommendedShipDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RecommendedAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastImportBatchId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestockRecommendation", x => new { x.MarketplaceId, x.Sku });
                    table.CheckConstraint("CK_RestockRecommendation_Quantity", "[RecommendedQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_RestockRecommendation_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestockRecommendation_Product_Sku",
                        column: x => x.Sku,
                        principalSchema: "core",
                        principalTable: "Product",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FbaRestockRow_ImportBatchId_RowNumber",
                schema: "stg",
                table: "FbaRestockRow",
                columns: new[] { "ImportBatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeStockMovement_MarketplaceId_OccurredAt",
                schema: "core",
                table: "HomeStockMovement",
                columns: new[] { "MarketplaceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeStockMovement_MarketplaceId_Sku_Id",
                schema: "core",
                table: "HomeStockMovement",
                columns: new[] { "MarketplaceId", "Sku", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeStockMovement_ReversesId",
                schema: "core",
                table: "HomeStockMovement",
                column: "ReversesId",
                unique: true,
                filter: "[ReversesId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HomeStockMovement_Sku",
                schema: "core",
                table: "HomeStockMovement",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_RestockRecommendation_Sku",
                schema: "core",
                table: "RestockRecommendation",
                column: "Sku");

            // Every existing balance becomes the first ledger entry, so Σ entries = home stock from day one.
            migrationBuilder.Sql("""
                INSERT core.HomeStockMovement (MarketplaceId, Sku, OccurredAt, Type, Units, BalanceAfter, Note, CreatedAt, CreatedBy)
                SELECT MarketplaceId, Sku, UpdatedAt, N'OpeningBalance', Quantity, Quantity, N'Balance when the ledger started', SYSDATETIMEOFFSET(), UpdatedBy
                FROM core.HomeStock
                WHERE Quantity > 0;
                """);

            migrationBuilder.Sql(SqlResource.Read("V008_InventoryWorksheet", "core.usp_PromoteImportBatch.sql"));
            migrationBuilder.Sql(SqlResource.Read("V008_InventoryWorksheet", "rpt.vw_InventoryPosition.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the previous view and procedure first so nothing references the dropped tables and columns.
            migrationBuilder.Sql(SqlResource.Read("V007_InventoryItems", "rpt.vw_InventoryPosition.sql"));
            migrationBuilder.Sql(SqlResource.Read("V006_InventoryDetail", "core.usp_PromoteImportBatch.sql"));

            migrationBuilder.DropTable(
                name: "FbaRestockRow",
                schema: "stg");

            migrationBuilder.DropTable(
                name: "HomeStockMovement",
                schema: "core");

            migrationBuilder.DropTable(
                name: "RestockRecommendation",
                schema: "core");

            migrationBuilder.DropColumn(
                name: "Color",
                schema: "core",
                table: "Product");
        }
    }
}
