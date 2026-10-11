using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Links each staging batch to its untouched source file in the raw blob landing zone (path + SHA-256).
    /// Nullable because batches staged before the landing zone existed have no stored file.
    /// </summary>
    public partial class AddRawFileToImportBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawFilePath",
                schema: "stg",
                table: "ImportBatch",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawFileSha256",
                schema: "stg",
                table: "ImportBatch",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatch_RawFileSha256",
                schema: "stg",
                table: "ImportBatch",
                column: "RawFileSha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportBatch_RawFileSha256",
                schema: "stg",
                table: "ImportBatch");

            migrationBuilder.DropColumn(
                name: "RawFilePath",
                schema: "stg",
                table: "ImportBatch");

            migrationBuilder.DropColumn(
                name: "RawFileSha256",
                schema: "stg",
                table: "ImportBatch");
        }
    }
}
