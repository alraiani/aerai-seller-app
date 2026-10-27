using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the FBA inventory planning report: <c>stg.FbaPlanningRow</c>, <c>core.InventoryHealth</c>, its promotion
    /// branch, its batch data range (a snapshot), and the stock age, excess, and fee columns on
    /// <c>rpt.vw_InventoryPosition</c> that restock planning uses to avoid long-term storage fees.
    /// </summary>
    public partial class AddInventoryHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FbaPlanningRow",
                schema: "stg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sku = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Asin = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Marketplace = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Age0To90 = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Age91To180 = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Age181To270 = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Age271To365 = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Age365Plus = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    EstimatedExcess = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    DaysOfSupply = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    StorageCostNextMonth = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AgedSurchargeNextMonth = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    RawLine = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FbaPlanningRow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FbaPlanningRow_ImportBatch_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "stg",
                        principalTable: "ImportBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryHealth",
                schema: "core",
                columns: table => new
                {
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Age0To90 = table.Column<int>(type: "int", nullable: false),
                    Age91To180 = table.Column<int>(type: "int", nullable: false),
                    Age181To270 = table.Column<int>(type: "int", nullable: false),
                    Age271To365 = table.Column<int>(type: "int", nullable: false),
                    Age365Plus = table.Column<int>(type: "int", nullable: false),
                    EstimatedExcess = table.Column<int>(type: "int", nullable: false),
                    DaysOfSupply = table.Column<int>(type: "int", nullable: true),
                    StorageCostNextMonth = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AgedSurchargeNextMonth = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    LastImportBatchId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryHealth", x => new { x.MarketplaceId, x.Sku });
                    table.CheckConstraint("CK_InventoryHealth_Units", "[Age0To90] >= 0 AND [Age91To180] >= 0 AND [Age181To270] >= 0 AND [Age271To365] >= 0 AND [Age365Plus] >= 0 AND [EstimatedExcess] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryHealth_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryHealth_Product_Sku",
                        column: x => x.Sku,
                        principalSchema: "core",
                        principalTable: "Product",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FbaPlanningRow_ImportBatchId_RowNumber",
                schema: "stg",
                table: "FbaPlanningRow",
                columns: new[] { "ImportBatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryHealth_Sku",
                schema: "core",
                table: "InventoryHealth",
                column: "Sku");

            migrationBuilder.Sql(SqlResource.Read("V022_InventoryHealth", "core.usp_PromoteImportBatch.sql"));
            migrationBuilder.Sql(SqlResource.Read("V022_InventoryHealth", "stg.usp_SetBatchDataRange.sql"));
            migrationBuilder.Sql(SqlResource.Read("V022_InventoryHealth", "rpt.vw_InventoryPosition.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The previous view and procedures first: they no longer reference the tables dropped below.
            migrationBuilder.Sql(SqlResource.Read("V018_AwdSnapshotDate", "rpt.vw_InventoryPosition.sql"));
            migrationBuilder.Sql(SqlResource.Read("V012_BatchDataRangeSettlementPeriod", "stg.usp_SetBatchDataRange.sql"));
            migrationBuilder.Sql(SqlResource.Read("V017_SettlementExchangeRates", "core.usp_PromoteImportBatch.sql"));

            migrationBuilder.DropTable(
                name: "FbaPlanningRow",
                schema: "stg");

            migrationBuilder.DropTable(
                name: "InventoryHealth",
                schema: "core");
        }
    }
}
