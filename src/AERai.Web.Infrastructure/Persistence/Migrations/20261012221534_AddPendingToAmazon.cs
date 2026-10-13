using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingToAmazon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AmazonBaselineAt",
                schema: "core",
                table: "HomeStockMovement",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmazonBaselineUnits",
                schema: "core",
                table: "HomeStockMovement",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReconciledAt",
                schema: "core",
                table: "HomeStockMovement",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReconciledBy",
                schema: "core",
                table: "HomeStockMovement",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReconciledUnits",
                schema: "core",
                table: "HomeStockMovement",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_HomeStockMovement_PendingToAmazon",
                schema: "core",
                table: "HomeStockMovement",
                columns: new[] { "MarketplaceId", "Sku" },
                filter: "[Type] = N'ShippedToAmazon' AND [Units] < 0 AND [ReconciledAt] IS NULL");

            migrationBuilder.Sql(SqlResource.Read("V016_PendingToAmazon", "rpt.vw_InventoryPosition.sql"));

            // Sends logged before this: closed if Amazon's latest snapshot is newer, otherwise kept pending
            // with that snapshot as their baseline. Reads the view above, so it runs after it.
            migrationBuilder.Sql(SqlResource.Read("V016_PendingToAmazon", "backfill-pending-sends.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("V010_AwdInventory", "rpt.vw_InventoryPosition.sql"));

            migrationBuilder.DropIndex(
                name: "IX_HomeStockMovement_PendingToAmazon",
                schema: "core",
                table: "HomeStockMovement");

            migrationBuilder.DropColumn(
                name: "AmazonBaselineAt",
                schema: "core",
                table: "HomeStockMovement");

            migrationBuilder.DropColumn(
                name: "AmazonBaselineUnits",
                schema: "core",
                table: "HomeStockMovement");

            migrationBuilder.DropColumn(
                name: "ReconciledAt",
                schema: "core",
                table: "HomeStockMovement");

            migrationBuilder.DropColumn(
                name: "ReconciledBy",
                schema: "core",
                table: "HomeStockMovement");

            migrationBuilder.DropColumn(
                name: "ReconciledUnits",
                schema: "core",
                table: "HomeStockMovement");
        }
    }
}
