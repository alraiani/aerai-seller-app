using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeferredTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeferredTransaction",
                schema: "core",
                columns: table => new
                {
                    TransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AmazonOrderId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PostedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    DeferralReason = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MaturityDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeferredTransaction", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_DeferredTransaction_Marketplace_MarketplaceId",
                        column: x => x.MarketplaceId,
                        principalSchema: "core",
                        principalTable: "Marketplace",
                        principalColumn: "MarketplaceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeferredTransaction_AmazonOrderId",
                schema: "core",
                table: "DeferredTransaction",
                column: "AmazonOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DeferredTransaction_MarketplaceId_Status",
                schema: "core",
                table: "DeferredTransaction",
                columns: new[] { "MarketplaceId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeferredTransaction",
                schema: "core");
        }
    }
}
