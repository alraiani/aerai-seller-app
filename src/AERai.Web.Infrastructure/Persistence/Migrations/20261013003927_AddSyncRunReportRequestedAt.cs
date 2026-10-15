using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncRunReportRequestedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncRun_NextCheckAt",
                schema: "ops",
                table: "SyncRun");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReportRequestedAt",
                schema: "ops",
                table: "SyncRun",
                type: "datetimeoffset",
                nullable: true);

            // Earlier runs asked Amazon for their report as they started, so their start time is the request
            // time; without it the first run after deploying could ask again inside the cooldown.
            migrationBuilder.Sql(
                """
                UPDATE ops.SyncRun
                SET ReportRequestedAt = StartedAt
                WHERE AmazonReportIds IS NOT NULL
                  AND ReportType NOT IN (N'Settlements', N'AwdInventory', N'OrdersApi');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SyncRun_NextCheckAt",
                schema: "ops",
                table: "SyncRun",
                column: "NextCheckAt",
                filter: "[NextCheckAt] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SyncRun_ReportType_MarketplaceId_ReportRequestedAt",
                schema: "ops",
                table: "SyncRun",
                columns: new[] { "ReportType", "MarketplaceId", "ReportRequestedAt" },
                filter: "[ReportRequestedAt] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncRun_NextCheckAt",
                schema: "ops",
                table: "SyncRun");

            migrationBuilder.DropIndex(
                name: "IX_SyncRun_ReportType_MarketplaceId_ReportRequestedAt",
                schema: "ops",
                table: "SyncRun");

            migrationBuilder.DropColumn(
                name: "ReportRequestedAt",
                schema: "ops",
                table: "SyncRun");

            migrationBuilder.CreateIndex(
                name: "IX_SyncRun_NextCheckAt",
                schema: "ops",
                table: "SyncRun",
                column: "NextCheckAt",
                filter: "[PendingReportId] IS NOT NULL");
        }
    }
}
