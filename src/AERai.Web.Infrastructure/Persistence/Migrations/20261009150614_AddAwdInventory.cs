using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAwdInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AwdInventorySnapshot",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OnHand = table.Column<int>(type: "int", nullable: false),
                    Inbound = table.Column<int>(type: "int", nullable: false),
                    AvailableDistributable = table.Column<int>(type: "int", nullable: false),
                    ReservedDistributable = table.Column<int>(type: "int", nullable: false),
                    Replenishment = table.Column<int>(type: "int", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwdInventorySnapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwdInventorySnapshot_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AwdInventorySnapshot_Product_Sku",
                        column: x => x.Sku,
                        principalSchema: "core",
                        principalTable: "Product",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AwdInventorySnapshot_MarketplaceId_Sku_SnapshotDate",
                schema: "core",
                table: "AwdInventorySnapshot",
                columns: new[] { "MarketplaceId", "Sku", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AwdInventorySnapshot_MarketplaceId_SnapshotDate",
                schema: "core",
                table: "AwdInventorySnapshot",
                columns: new[] { "MarketplaceId", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AwdInventorySnapshot_Sku",
                schema: "core",
                table: "AwdInventorySnapshot",
                column: "Sku");

            migrationBuilder.Sql(SqlResource.Read("V010_AwdInventory", "rpt.vw_InventoryPosition.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("V008_InventoryWorksheet", "rpt.vw_InventoryPosition.sql"));

            // The previous code cannot read the AwdInventory report type, so its schedules and runs go too.
            migrationBuilder.Sql("DELETE FROM ops.SyncRun WHERE ReportType = N'AwdInventory';");
            migrationBuilder.Sql("DELETE FROM ops.SyncSchedule WHERE ReportType = N'AwdInventory';");

            migrationBuilder.DropTable(
                name: "AwdInventorySnapshot",
                schema: "core");
        }
    }
}
