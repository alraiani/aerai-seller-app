using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialTransactionLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialTransaction",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PostedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AmazonOrderId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SettlementId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialTransaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialTransaction_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialTransactionLine",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FinancialTransactionId = table.Column<long>(type: "bigint", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Asin = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Units = table.Column<int>(type: "int", nullable: false),
                    BreakdownPath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialTransactionLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialTransactionLine_FinancialTransaction_FinancialTransactionId",
                        column: x => x.FinancialTransactionId,
                        principalSchema: "core",
                        principalTable: "FinancialTransaction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransaction_AmazonOrderId",
                schema: "core",
                table: "FinancialTransaction",
                column: "AmazonOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransaction_MarketplaceId_PostedDate",
                schema: "core",
                table: "FinancialTransaction",
                columns: new[] { "MarketplaceId", "PostedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransaction_MarketplaceId_TransactionId",
                schema: "core",
                table: "FinancialTransaction",
                columns: new[] { "MarketplaceId", "TransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransaction_SettlementId",
                schema: "core",
                table: "FinancialTransaction",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactionLine_FinancialTransactionId",
                schema: "core",
                table: "FinancialTransactionLine",
                column: "FinancialTransactionId")
                .Annotation("SqlServer:Include", new[] { "Sku", "Units", "BreakdownPath", "Amount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialTransactionLine",
                schema: "core");

            migrationBuilder.DropTable(
                name: "FinancialTransaction",
                schema: "core");
        }
    }
}
