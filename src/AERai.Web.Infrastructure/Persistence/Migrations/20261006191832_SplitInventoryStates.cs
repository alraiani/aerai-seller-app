using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Keeps Amazon's inventory detail instead of rolling it up: inbound is stored per stage (working,
    /// shipped, receiving), and a new reserved-inventory report (stg.FbaReservedRow) splits reserved
    /// stock into customer orders, FC transfers, and FC processing. The inventory view now exposes
    /// one column per state; sales velocity moves out of the view into the application.
    /// </summary>
    public partial class SplitInventoryStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventorySnapshot_State",
                schema: "core",
                table: "InventorySnapshot");

            migrationBuilder.CreateTable(
                name: "FbaReservedRow",
                schema: "stg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sku = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Asin = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ReservedQuantity = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ReservedCustomerOrders = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ReservedFcTransfers = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ReservedFcProcessing = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    RawLine = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FbaReservedRow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FbaReservedRow_ImportBatch_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "stg",
                        principalTable: "ImportBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventorySnapshot_State",
                schema: "core",
                table: "InventorySnapshot",
                sql: "[State] IN (N'Available', N'InboundWorking', N'InboundShipped', N'InboundReceiving', N'Inbound', N'ReservedCustomerOrder', N'ReservedFcTransfer', N'ReservedFcProcessing', N'Reserved', N'Unfulfillable')");

            migrationBuilder.CreateIndex(
                name: "IX_FbaReservedRow_ImportBatchId_RowNumber",
                schema: "stg",
                table: "FbaReservedRow",
                columns: new[] { "ImportBatchId", "RowNumber" },
                unique: true);

            // Existing snapshots keep their unsplit Inbound/Reserved totals (the stage detail was never
            // stored); the next FBA reports fill in the breakdown.
            migrationBuilder.Sql(SqlResource.Read("V006_InventoryDetail", "stg.ufn_NormalizeInventoryState.sql"));
            migrationBuilder.Sql(SqlResource.Read("V006_InventoryDetail", "core.usp_PromoteImportBatch.sql"));
            migrationBuilder.Sql(SqlResource.Read("V006_InventoryDetail", "rpt.vw_InventoryPosition.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("V005_Marketplaces", "rpt.vw_InventoryPosition.sql"));
            migrationBuilder.Sql(SqlResource.Read("V005_Marketplaces", "core.usp_PromoteImportBatch.sql"));
            migrationBuilder.Sql(SqlResource.Read("V001_Initial", "stg.ufn_NormalizeInventoryState.sql"));

            // The previous version has no reserved report, so its schedules, history, and batches go.
            migrationBuilder.Sql("""
                DELETE FROM ops.IngestedReport WHERE ReportType = N'FbaReservedInventory';
                DELETE FROM ops.SyncRun WHERE ReportType = N'FbaReservedInventory';
                DELETE FROM ops.SyncSchedule WHERE ReportType = N'FbaReservedInventory';
                DELETE FROM stg.ImportBatch WHERE Source = N'FbaReservedInventory';
                """);

            // Roll the detailed states back up into the four previous states.
            migrationBuilder.Sql("""
                SELECT MarketplaceId, SnapshotDate, Sku,
                       CASE WHEN State LIKE N'Inbound%' THEN N'Inbound' WHEN State LIKE N'Reserved%' THEN N'Reserved' ELSE State END AS State,
                       SUM(Quantity) AS Quantity, MAX(LastImportBatchId) AS LastImportBatchId
                INTO #RolledUp
                FROM core.InventorySnapshot
                WHERE State LIKE N'Inbound%' OR State LIKE N'Reserved%'
                GROUP BY MarketplaceId, SnapshotDate, Sku,
                         CASE WHEN State LIKE N'Inbound%' THEN N'Inbound' WHEN State LIKE N'Reserved%' THEN N'Reserved' ELSE State END;

                DELETE FROM core.InventorySnapshot WHERE State LIKE N'Inbound%' OR State LIKE N'Reserved%';

                INSERT core.InventorySnapshot (MarketplaceId, SnapshotDate, Sku, State, Quantity, LastImportBatchId)
                SELECT MarketplaceId, SnapshotDate, Sku, State, Quantity, LastImportBatchId FROM #RolledUp;
                """);

            migrationBuilder.DropTable(
                name: "FbaReservedRow",
                schema: "stg");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventorySnapshot_State",
                schema: "core",
                table: "InventorySnapshot");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventorySnapshot_State",
                schema: "core",
                table: "InventorySnapshot",
                sql: "[State] IN (N'Available', N'Inbound', N'Reserved', N'Unfulfillable')");
        }
    }
}
