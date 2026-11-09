using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCostHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductCostHistory",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CostOfGoods = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCostHistory", x => x.Id);
                    table.CheckConstraint("CK_ProductCostHistory_NonNegative", "[CostOfGoods] IS NULL OR [CostOfGoods] >= 0");
                    table.ForeignKey(
                        name: "FK_ProductCostHistory_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductCostHistory_Product_Sku",
                        column: x => x.Sku,
                        principalSchema: "core",
                        principalTable: "Product",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCostHistory_MarketplaceId_Sku_EffectiveFrom",
                schema: "core",
                table: "ProductCostHistory",
                columns: new[] { "MarketplaceId", "Sku", "EffectiveFrom" })
                .Annotation("SqlServer:Include", new[] { "CostOfGoods" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCostHistory_Sku",
                schema: "core",
                table: "ProductCostHistory",
                column: "Sku");

            // Costs set before history existed apply to every past sale until they are next changed:
            // the best figure available, and the same one the Orders page has always used.
            migrationBuilder.Sql("""
                INSERT INTO core.ProductCostHistory (Sku, MarketplaceId, CostOfGoods, EffectiveFrom, ChangedBy)
                SELECT c.Sku, c.MarketplaceId, c.CostOfGoods, '2000-01-01T00:00:00+00:00', N'system'
                FROM core.ProductCost AS c;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductCostHistory",
                schema: "core");
        }
    }
}
