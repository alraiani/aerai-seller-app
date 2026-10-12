using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the user-maintained parts of an inventory item: product families (core.ProductFamily,
    /// Product.FamilyId), a product picture (Product.ImagePath/ImageContentType, stored in the
    /// product-images blob container), and per-marketplace home stock (core.HomeStock). The inventory
    /// view gains those columns and includes SKUs held only at home.
    /// </summary>
    public partial class AddInventoryItemDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FamilyId",
                schema: "core",
                table: "Product",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                schema: "core",
                table: "Product",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                schema: "core",
                table: "Product",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HomeStock",
                schema: "core",
                columns: table => new
                {
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeStock", x => new { x.Sku, x.MarketplaceId });
                    table.CheckConstraint("CK_HomeStock_NonNegative", "[Quantity] >= 0");
                    table.ForeignKey(
                        name: "FK_HomeStock_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HomeStock_Product_Sku",
                        column: x => x.Sku,
                        principalSchema: "core",
                        principalTable: "Product",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductFamily",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductFamily", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Product_FamilyId",
                schema: "core",
                table: "Product",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeStock_MarketplaceId",
                schema: "core",
                table: "HomeStock",
                column: "MarketplaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductFamily_Name",
                schema: "core",
                table: "ProductFamily",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Product_ProductFamily_FamilyId",
                schema: "core",
                table: "Product",
                column: "FamilyId",
                principalSchema: "core",
                principalTable: "ProductFamily",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(SqlResource.Read("V007_InventoryItems", "rpt.vw_InventoryPosition.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the previous view first so nothing references the dropped tables and columns.
            migrationBuilder.Sql(SqlResource.Read("V006_InventoryDetail", "rpt.vw_InventoryPosition.sql"));

            migrationBuilder.DropForeignKey(
                name: "FK_Product_ProductFamily_FamilyId",
                schema: "core",
                table: "Product");

            migrationBuilder.DropTable(
                name: "HomeStock",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ProductFamily",
                schema: "core");

            migrationBuilder.DropIndex(
                name: "IX_Product_FamilyId",
                schema: "core",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                schema: "core",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                schema: "core",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                schema: "core",
                table: "Product");
        }
    }
}
