using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds core.LeadTimeProfile: per-marketplace supplier, prep, transit, safety, and target-cover
    /// overrides for restock planning (blank fields use the app-wide defaults).
    /// </summary>
    public partial class AddLeadTimeProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeadTimeProfile",
                schema: "core",
                columns: table => new
                {
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SupplierLeadTimeDays = table.Column<int>(type: "int", nullable: true),
                    PrepTimeDays = table.Column<int>(type: "int", nullable: true),
                    TransitDays = table.Column<int>(type: "int", nullable: true),
                    SafetyStockDays = table.Column<int>(type: "int", nullable: true),
                    TargetStockDays = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadTimeProfile", x => new { x.Sku, x.MarketplaceId });
                    table.CheckConstraint("CK_LeadTimeProfile_PrepTimeDays", "[PrepTimeDays] BETWEEN 0 AND 730");
                    table.CheckConstraint("CK_LeadTimeProfile_SafetyStockDays", "[SafetyStockDays] BETWEEN 0 AND 730");
                    table.CheckConstraint("CK_LeadTimeProfile_SupplierLeadTimeDays", "[SupplierLeadTimeDays] BETWEEN 0 AND 730");
                    table.CheckConstraint("CK_LeadTimeProfile_TargetStockDays", "[TargetStockDays] BETWEEN 1 AND 730");
                    table.CheckConstraint("CK_LeadTimeProfile_TransitDays", "[TransitDays] BETWEEN 0 AND 730");
                    table.ForeignKey(
                        name: "FK_LeadTimeProfile_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeadTimeProfile_Product_Sku",
                        column: x => x.Sku,
                        principalSchema: "core",
                        principalTable: "Product",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeadTimeProfile_MarketplaceId",
                schema: "core",
                table: "LeadTimeProfile",
                column: "MarketplaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadTimeProfile",
                schema: "core");
        }
    }
}
