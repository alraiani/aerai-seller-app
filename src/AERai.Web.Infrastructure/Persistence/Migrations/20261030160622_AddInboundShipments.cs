using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShipsFromHomeStock",
                schema: "core",
                table: "ProductFamily",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Handkerchiefs are the families sent into Amazon from home today; others are switched on in the Families dialog.
            migrationBuilder.Sql("UPDATE core.ProductFamily SET ShipsFromHomeStock = 1 WHERE Name LIKE N'%handkerchief%';");

            migrationBuilder.CreateTable(
                name: "InboundShipment",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ShipmentConfirmationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AmazonShipmentId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InboundPlanId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AmazonStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PlanCreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Review = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundShipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundShipment_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InboundShipmentItem",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InboundShipmentId = table.Column<long>(type: "bigint", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundShipmentItem", x => x.Id);
                    table.CheckConstraint("CK_InboundShipmentItem_Quantity", "Quantity >= 0");
                    table.ForeignKey(
                        name: "FK_InboundShipmentItem_InboundShipment_InboundShipmentId",
                        column: x => x.InboundShipmentId,
                        principalSchema: "core",
                        principalTable: "InboundShipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboundShipment_MarketplaceId_Review",
                schema: "core",
                table: "InboundShipment",
                columns: new[] { "MarketplaceId", "Review" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundShipment_MarketplaceId_ShipmentConfirmationId",
                schema: "core",
                table: "InboundShipment",
                columns: new[] { "MarketplaceId", "ShipmentConfirmationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundShipmentItem_InboundShipmentId_Sku",
                schema: "core",
                table: "InboundShipmentItem",
                columns: new[] { "InboundShipmentId", "Sku" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboundShipmentItem",
                schema: "core");

            migrationBuilder.DropTable(
                name: "InboundShipment",
                schema: "core");

            migrationBuilder.DropColumn(
                name: "ShipsFromHomeStock",
                schema: "core",
                table: "ProductFamily");
        }
    }
}
