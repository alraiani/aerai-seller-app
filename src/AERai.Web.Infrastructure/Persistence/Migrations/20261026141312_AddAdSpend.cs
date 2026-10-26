using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds <c>core.AdSpend</c>: Sponsored Products spend per (marketplace, day, SKU, campaign) from the Amazon
    /// Ads API, written by the Ads spend sync (<c>AmazonReportType.AdsSpend</c>).
    /// </summary>
    public partial class AddAdSpend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdSpend",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Asin = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CampaignId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CampaignName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Impressions = table.Column<long>(type: "bigint", nullable: false),
                    Clicks = table.Column<long>(type: "bigint", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AttributedSales = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AttributedOrders = table.Column<int>(type: "int", nullable: false),
                    AttributedUnits = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdSpend", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdSpend_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdSpend_Product_Sku",
                        column: x => x.Sku,
                        principalSchema: "core",
                        principalTable: "Product",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdSpend_MarketplaceId_Date_Sku_CampaignId",
                schema: "core",
                table: "AdSpend",
                columns: new[] { "MarketplaceId", "Date", "Sku", "CampaignId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdSpend_Sku",
                schema: "core",
                table: "AdSpend",
                column: "Sku");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The previous code cannot read the AdsSpend report type, so its schedules and runs go too.
            migrationBuilder.Sql("DELETE FROM ops.SyncRun WHERE ReportType = N'AdsSpend';");
            migrationBuilder.Sql("DELETE FROM ops.SyncSchedule WHERE ReportType = N'AdsSpend';");

            migrationBuilder.DropTable(
                name: "AdSpend",
                schema: "core");
        }
    }
}
