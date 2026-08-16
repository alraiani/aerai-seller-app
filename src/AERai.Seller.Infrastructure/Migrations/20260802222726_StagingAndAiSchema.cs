using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Seller.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StagingAndAiSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemandForecasts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sku = table.Column<string>(type: "TEXT", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DailySalesVelocity = table.Column<decimal>(type: "TEXT", nullable: false),
                    SellThroughEligibleStock = table.Column<int>(type: "INTEGER", nullable: false),
                    DaysOfSupply = table.Column<decimal>(type: "TEXT", nullable: false),
                    ProjectedStockoutDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandForecasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeadTimeProfiles",
                columns: table => new
                {
                    Sku = table.Column<string>(type: "TEXT", nullable: false),
                    SupplierLeadTimeDays = table.Column<int>(type: "INTEGER", nullable: false),
                    PrepTimeDays = table.Column<int>(type: "INTEGER", nullable: false),
                    FbaTransitDays = table.Column<int>(type: "INTEGER", nullable: false),
                    SafetyStockDays = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadTimeProfiles", x => x.Sku);
                });

            migrationBuilder.CreateTable(
                name: "ReplenishmentRecommendations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sku = table.Column<string>(type: "TEXT", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RecommendedOrderQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    RecommendedOrderBy = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    UnitsDueOutOfPrep = table.Column<int>(type: "INTEGER", nullable: false),
                    PrepDueBy = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    UnitsToShipToFba = table.Column<int>(type: "INTEGER", nullable: false),
                    ShipToFbaBy = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DaysUntilActionNeeded = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplenishmentRecommendations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemandForecasts_Sku_ComputedAt",
                table: "DemandForecasts",
                columns: new[] { "Sku", "ComputedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReplenishmentRecommendations_Sku_ComputedAt",
                table: "ReplenishmentRecommendations",
                columns: new[] { "Sku", "ComputedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandForecasts");

            migrationBuilder.DropTable(
                name: "LeadTimeProfiles");

            migrationBuilder.DropTable(
                name: "ReplenishmentRecommendations");
        }
    }
}
