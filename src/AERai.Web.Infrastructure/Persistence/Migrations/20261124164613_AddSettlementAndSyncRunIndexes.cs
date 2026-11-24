using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Indexes the live Query Store asked for: core.SettlementLine by settlement now covers every column the
    /// summary-table refresh and the settlement pages read (it runs before the backfill so the backfill uses
    /// it), and ops.SyncRun gets one for the scheduler's abandoned-run check.
    /// </summary>
    public partial class AddSettlementAndSyncRunIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettlementLine_SettlementId",
                schema: "core",
                table: "SettlementLine");

            migrationBuilder.CreateIndex(
                name: "IX_SyncRun_Status_NextCheckAt_StartedAt",
                schema: "ops",
                table: "SyncRun",
                columns: new[] { "Status", "NextCheckAt", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementLine_SettlementId",
                schema: "core",
                table: "SettlementLine",
                column: "SettlementId")
                .Annotation("SqlServer:Include", new[] { "PostedDate", "TransactionType", "AmazonOrderId", "OrderItemCode", "Quantity", "Sku", "AmountType", "AmountDescription", "Amount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncRun_Status_NextCheckAt_StartedAt",
                schema: "ops",
                table: "SyncRun");

            migrationBuilder.DropIndex(
                name: "IX_SettlementLine_SettlementId",
                schema: "core",
                table: "SettlementLine");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementLine_SettlementId",
                schema: "core",
                table: "SettlementLine",
                column: "SettlementId");
        }
    }
}
