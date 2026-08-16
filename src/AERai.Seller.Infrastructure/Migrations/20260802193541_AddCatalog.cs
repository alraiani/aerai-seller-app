using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Seller.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogParents",
                columns: table => new
                {
                    ParentAsin = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    SyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogParents", x => x.ParentAsin);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItems",
                columns: table => new
                {
                    Asin = table.Column<string>(type: "TEXT", nullable: false),
                    ParentAsin = table.Column<string>(type: "TEXT", nullable: true),
                    Sku = table.Column<string>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Brand = table.Column<string>(type: "TEXT", nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    SyncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Asin);
                    table.ForeignKey(
                        name: "FK_CatalogItems_CatalogParents_ParentAsin",
                        column: x => x.ParentAsin,
                        principalTable: "CatalogParents",
                        principalColumn: "ParentAsin",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_ParentAsin",
                table: "CatalogItems",
                column: "ParentAsin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogItems");

            migrationBuilder.DropTable(
                name: "CatalogParents");
        }
    }
}
