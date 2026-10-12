using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SkipOtherMarketplaceOrderLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SkippedRowCount",
                schema: "stg",
                table: "ImportBatch",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(SqlResource.Read("V009_OrdersOtherMarketplaces", "core.usp_PromoteImportBatch.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The previous procedure first, so nothing references the column when it is dropped.
            migrationBuilder.Sql(SqlResource.Read("V008_InventoryWorksheet", "core.usp_PromoteImportBatch.sql"));

            migrationBuilder.DropColumn(
                name: "SkippedRowCount",
                schema: "stg",
                table: "ImportBatch");
        }
    }
}
