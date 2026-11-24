using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Covering indexes for sales reads through rpt.vw_SalesLine: core.Order by (marketplace, purchase date)
    /// replaces the plain marketplace index, and the order-item key also carries quantity and price.
    /// </summary>
    public partial class AddSalesReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderItem_OrderId_Sku",
                schema: "core",
                table: "OrderItem");

            migrationBuilder.DropIndex(
                name: "IX_Order_MarketplaceId",
                schema: "core",
                table: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItem_OrderId_Sku",
                schema: "core",
                table: "OrderItem",
                columns: new[] { "OrderId", "Sku" },
                unique: true)
                .Annotation("SqlServer:Include", new[] { "Quantity", "ItemPrice" });

            migrationBuilder.CreateIndex(
                name: "IX_Order_MarketplaceId_PurchaseDate",
                schema: "core",
                table: "Order",
                columns: new[] { "MarketplaceId", "PurchaseDate" })
                .Annotation("SqlServer:Include", new[] { "AmazonOrderId", "OrderStatus", "Currency" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderItem_OrderId_Sku",
                schema: "core",
                table: "OrderItem");

            migrationBuilder.DropIndex(
                name: "IX_Order_MarketplaceId_PurchaseDate",
                schema: "core",
                table: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItem_OrderId_Sku",
                schema: "core",
                table: "OrderItem",
                columns: new[] { "OrderId", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Order_MarketplaceId",
                schema: "core",
                table: "Order",
                column: "MarketplaceId");
        }
    }
}
