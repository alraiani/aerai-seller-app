using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds in-app stock alerts: ops.StockAlert (at most one open alert per marketplace and SKU,
    /// enforced by a filtered unique index) and ops.StockAlertRead (per-user read state).
    /// </summary>
    public partial class AddStockAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockAlert",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RaisedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAlert", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockAlert_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAlert_Product_Sku",
                        column: x => x.Sku,
                        principalSchema: "core",
                        principalTable: "Product",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockAlertRead",
                schema: "ops",
                columns: table => new
                {
                    StockAlertId = table.Column<long>(type: "bigint", nullable: false),
                    UserEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAlertRead", x => new { x.StockAlertId, x.UserEmail });
                    table.ForeignKey(
                        name: "FK_StockAlertRead_StockAlert_StockAlertId",
                        column: x => x.StockAlertId,
                        principalSchema: "ops",
                        principalTable: "StockAlert",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockAlert_MarketplaceId_ResolvedAt",
                schema: "ops",
                table: "StockAlert",
                columns: new[] { "MarketplaceId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAlert_Sku",
                schema: "ops",
                table: "StockAlert",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "UX_StockAlert_Open",
                schema: "ops",
                table: "StockAlert",
                columns: new[] { "MarketplaceId", "Sku" },
                unique: true,
                filter: "[ResolvedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockAlertRead",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "StockAlert",
                schema: "ops");
        }
    }
}
