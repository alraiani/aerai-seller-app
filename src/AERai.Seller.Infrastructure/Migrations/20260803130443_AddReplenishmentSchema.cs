using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Seller.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReplenishmentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetStockDays",
                table: "LeadTimeProfiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AwdInventorySnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sku = table.Column<string>(type: "TEXT", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TotalOnhandQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalInboundQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    AvailableDistributableQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ReservedDistributableQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ReplenishmentQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwdInventorySnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AwdInventorySnapshots_Sku_SnapshotDate",
                table: "AwdInventorySnapshots",
                columns: new[] { "Sku", "SnapshotDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AwdInventorySnapshots");

            migrationBuilder.DropColumn(
                name: "TargetStockDays",
                table: "LeadTimeProfiles");
        }
    }
}
