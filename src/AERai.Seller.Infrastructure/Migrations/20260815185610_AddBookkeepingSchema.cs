using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Seller.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookkeepingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookkeepingAccountMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AmountType = table.Column<string>(type: "TEXT", nullable: false),
                    AmountDescription = table.Column<string>(type: "TEXT", nullable: false),
                    QuickBooksAccountName = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookkeepingAccountMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookkeepingExportRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SettlementId = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    NetTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DepositDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookkeepingExportRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookkeepingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DepositAccountName = table.Column<string>(type: "TEXT", nullable: true),
                    ExportFolderPath = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookkeepingSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SettlementLineItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SettlementId = table.Column<string>(type: "TEXT", nullable: false),
                    AmazonOrderId = table.Column<string>(type: "TEXT", nullable: true),
                    Sku = table.Column<string>(type: "TEXT", nullable: true),
                    AmountType = table.Column<string>(type: "TEXT", nullable: false),
                    AmountDescription = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", nullable: false),
                    PostedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementLineItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookkeepingAccountMappings_AmountType_AmountDescription",
                table: "BookkeepingAccountMappings",
                columns: new[] { "AmountType", "AmountDescription" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookkeepingExportRecords_SettlementId",
                table: "BookkeepingExportRecords",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementLineItems_SettlementId",
                table: "SettlementLineItems",
                column: "SettlementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookkeepingAccountMappings");

            migrationBuilder.DropTable(
                name: "BookkeepingExportRecords");

            migrationBuilder.DropTable(
                name: "BookkeepingSettings");

            migrationBuilder.DropTable(
                name: "SettlementLineItems");
        }
    }
}
