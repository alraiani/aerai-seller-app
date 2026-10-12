using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Keeps order-item-code and quantity-purchased on settlement lines (staging and core), so the
    /// settlement views can show units sold and the price per item. Existing lines get the new
    /// columns by reprocessing their raw files; until then Units is 0 and the price is the whole principal.
    /// </summary>
    public partial class AddSettlementUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderItemCode",
                schema: "stg",
                table: "SettlementLine",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuantityPurchased",
                schema: "stg",
                table: "SettlementLine",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderItemCode",
                schema: "core",
                table: "SettlementLine",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                schema: "core",
                table: "SettlementLine",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(SqlResource.Read("V014_SettlementUnits", "core.usp_PromoteImportBatch.sql"));
            migrationBuilder.Sql(SqlResource.Read("V014_SettlementUnits", "rpt.vw_SettlementLine.sql"));
            migrationBuilder.Sql(SqlResource.Read("V014_SettlementUnits", "rpt.vw_SettlementSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V014_SettlementUnits", "rpt.vw_SettlementSkuSummary.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Procedure and views first, so nothing references the columns when they are dropped.
            migrationBuilder.Sql(SqlResource.Read("V009_OrdersOtherMarketplaces", "core.usp_PromoteImportBatch.sql"));
            migrationBuilder.Sql(SqlResource.Read("V013_SettlementDrillDown", "rpt.vw_SettlementLine.sql"));
            migrationBuilder.Sql(SqlResource.Read("V013_SettlementDrillDown", "rpt.vw_SettlementSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V013_SettlementDrillDown", "rpt.vw_SettlementSkuSummary.sql"));

            migrationBuilder.DropColumn(
                name: "OrderItemCode",
                schema: "stg",
                table: "SettlementLine");

            migrationBuilder.DropColumn(
                name: "QuantityPurchased",
                schema: "stg",
                table: "SettlementLine");

            migrationBuilder.DropColumn(
                name: "OrderItemCode",
                schema: "core",
                table: "SettlementLine");

            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "core",
                table: "SettlementLine");
        }
    }
}
