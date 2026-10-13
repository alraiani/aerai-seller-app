using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Keeps a settlement's deposit date and the exchange rate that converts a settlement paid in another
    /// currency (the CAD and MXN payouts Amazon lists with the United States) into its marketplace's
    /// currency; points Canada's settlements at the United States; and converts the settlement views.
    /// </summary>
    public partial class AddSettlementExchangeRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DepositDate",
                schema: "stg",
                table: "SettlementLine",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DepositDate",
                schema: "core",
                table: "Settlement",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementMarketplaceId",
                schema: "core",
                table: "Marketplace",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                schema: "core",
                table: "BookkeepingExport",
                type: "decimal(19,10)",
                precision: 19,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalCurrency",
                schema: "core",
                table: "BookkeepingExport",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalNetTotal",
                schema: "core",
                table: "BookkeepingExport",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SettlementExchangeRate",
                schema: "core",
                columns: table => new
                {
                    SettlementId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FromCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ToCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    MarketRate = table.Column<decimal>(type: "decimal(19,10)", precision: 19, scale: 10, nullable: true),
                    MarketRateDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MarketSource = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MarketFetchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ManualRate = table.Column<decimal>(type: "decimal(19,10)", precision: 19, scale: 10, nullable: true),
                    ManualHomeAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ManualBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ManualAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementExchangeRate", x => x.SettlementId);
                    table.ForeignKey(
                        name: "FK_SettlementExchangeRate_Settlement_SettlementId",
                        column: x => x.SettlementId,
                        principalSchema: "core",
                        principalTable: "Settlement",
                        principalColumn: "SettlementId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "Marketplace",
                keyColumn: "MarketplaceId",
                keyValue: "A1F83G8C2ARO7P",
                column: "SettlementMarketplaceId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "core",
                table: "Marketplace",
                keyColumn: "MarketplaceId",
                keyValue: "A2EUQ1WTGCTBG2",
                column: "SettlementMarketplaceId",
                value: "ATVPDKIKX0DER");

            migrationBuilder.UpdateData(
                schema: "core",
                table: "Marketplace",
                keyColumn: "MarketplaceId",
                keyValue: "ATVPDKIKX0DER",
                column: "SettlementMarketplaceId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Marketplace_SettlementMarketplaceId",
                schema: "core",
                table: "Marketplace",
                column: "SettlementMarketplaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Marketplace_Marketplace_SettlementMarketplaceId",
                schema: "core",
                table: "Marketplace",
                column: "SettlementMarketplaceId",
                principalSchema: "core",
                principalTable: "Marketplace",
                principalColumn: "MarketplaceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(SqlResource.Read("V017_SettlementExchangeRates", "core.usp_PromoteImportBatch.sql"));
            migrationBuilder.Sql(SqlResource.Read("V017_SettlementExchangeRates", "rpt.vw_SettlementRate.sql"));
            migrationBuilder.Sql(SqlResource.Read("V017_SettlementExchangeRates", "rpt.vw_SettlementLine.sql"));
            migrationBuilder.Sql(SqlResource.Read("V017_SettlementExchangeRates", "rpt.vw_SettlementSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V017_SettlementExchangeRates", "rpt.vw_SettlementSkuSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V017_SettlementExchangeRates", "backfill-deposit-date.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The views and procedure go back to V014 before the columns and table they read are dropped.
            migrationBuilder.Sql(SqlResource.Read("V014_SettlementUnits", "core.usp_PromoteImportBatch.sql"));
            migrationBuilder.Sql(SqlResource.Read("V014_SettlementUnits", "rpt.vw_SettlementLine.sql"));
            migrationBuilder.Sql(SqlResource.Read("V014_SettlementUnits", "rpt.vw_SettlementSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V014_SettlementUnits", "rpt.vw_SettlementSkuSummary.sql"));
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt.vw_SettlementRate;");

            migrationBuilder.DropForeignKey(
                name: "FK_Marketplace_Marketplace_SettlementMarketplaceId",
                schema: "core",
                table: "Marketplace");

            migrationBuilder.DropTable(
                name: "SettlementExchangeRate",
                schema: "core");

            migrationBuilder.DropIndex(
                name: "IX_Marketplace_SettlementMarketplaceId",
                schema: "core",
                table: "Marketplace");

            migrationBuilder.DropColumn(
                name: "DepositDate",
                schema: "stg",
                table: "SettlementLine");

            migrationBuilder.DropColumn(
                name: "DepositDate",
                schema: "core",
                table: "Settlement");

            migrationBuilder.DropColumn(
                name: "SettlementMarketplaceId",
                schema: "core",
                table: "Marketplace");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                schema: "core",
                table: "BookkeepingExport");

            migrationBuilder.DropColumn(
                name: "OriginalCurrency",
                schema: "core",
                table: "BookkeepingExport");

            migrationBuilder.DropColumn(
                name: "OriginalNetTotal",
                schema: "core",
                table: "BookkeepingExport");
        }
    }
}
