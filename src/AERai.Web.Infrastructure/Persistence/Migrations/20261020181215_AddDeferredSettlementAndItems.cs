using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeferredSettlementAndItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SettlementId",
                schema: "core",
                table: "DeferredTransaction",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeferredTransactionItem",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Asin = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Principal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeferredTransactionItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeferredTransactionItem_DeferredTransaction_TransactionId",
                        column: x => x.TransactionId,
                        principalSchema: "core",
                        principalTable: "DeferredTransaction",
                        principalColumn: "TransactionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeferredTransaction_SettlementId",
                schema: "core",
                table: "DeferredTransaction",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_DeferredTransactionItem_TransactionId",
                schema: "core",
                table: "DeferredTransactionItem",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeferredTransactionItem",
                schema: "core");

            migrationBuilder.DropIndex(
                name: "IX_DeferredTransaction_SettlementId",
                schema: "core",
                table: "DeferredTransaction");

            migrationBuilder.DropColumn(
                name: "SettlementId",
                schema: "core",
                table: "DeferredTransaction");
        }
    }
}
