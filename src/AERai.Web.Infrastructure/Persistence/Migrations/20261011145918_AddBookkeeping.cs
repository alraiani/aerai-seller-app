using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookkeeping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookkeepingAccountMapping",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AmountType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AmountDescription = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    QuickBooksAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookkeepingAccountMapping", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookkeepingExport",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SettlementId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepositAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NetTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    PeriodStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookkeepingExport", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookkeepingExport_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookkeepingExport_Settlement_SettlementId",
                        column: x => x.SettlementId,
                        principalSchema: "core",
                        principalTable: "Settlement",
                        principalColumn: "SettlementId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookkeepingSettings",
                schema: "core",
                columns: table => new
                {
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DepositAccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookkeepingSettings", x => x.MarketplaceId);
                    table.ForeignKey(
                        name: "FK_BookkeepingSettings_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookkeepingAccountMapping_AmountType_AmountDescription",
                schema: "core",
                table: "BookkeepingAccountMapping",
                columns: new[] { "AmountType", "AmountDescription" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookkeepingExport_MarketplaceId_GeneratedAt",
                schema: "core",
                table: "BookkeepingExport",
                columns: new[] { "MarketplaceId", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BookkeepingExport_SettlementId",
                schema: "core",
                table: "BookkeepingExport",
                column: "SettlementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookkeepingAccountMapping",
                schema: "core");

            migrationBuilder.DropTable(
                name: "BookkeepingExport",
                schema: "core");

            migrationBuilder.DropTable(
                name: "BookkeepingSettings",
                schema: "core");
        }
    }
}
