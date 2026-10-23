using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CorrectsId",
                schema: "core",
                table: "HomeStockMovement",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeStockMovement_CorrectsId",
                schema: "core",
                table: "HomeStockMovement",
                column: "CorrectsId",
                unique: true,
                filter: "[CorrectsId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_HomeStockMovement_HomeStockMovement_CorrectsId",
                schema: "core",
                table: "HomeStockMovement",
                column: "CorrectsId",
                principalSchema: "core",
                principalTable: "HomeStockMovement",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HomeStockMovement_HomeStockMovement_CorrectsId",
                schema: "core",
                table: "HomeStockMovement");

            migrationBuilder.DropIndex(
                name: "IX_HomeStockMovement_CorrectsId",
                schema: "core",
                table: "HomeStockMovement");

            migrationBuilder.DropColumn(
                name: "CorrectsId",
                schema: "core",
                table: "HomeStockMovement");
        }
    }
}
