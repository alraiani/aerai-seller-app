using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds rpt.vw_SettlementLine (every settlement line with its bucket, SKU and sale price) and
    /// rpt.vw_SettlementSkuSummary (the per-SKU/sale-price breakdown), and rebuilds
    /// rpt.vw_SettlementSummary on top of the line view so one rule buckets every page.
    /// </summary>
    public partial class AddSettlementDrillDownViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("V013_SettlementDrillDown", "rpt.vw_SettlementLine.sql"));
            migrationBuilder.Sql(SqlResource.Read("V013_SettlementDrillDown", "rpt.vw_SettlementSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V013_SettlementDrillDown", "rpt.vw_SettlementSkuSummary.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt.vw_SettlementSkuSummary;");

            // Restore the summary's previous definition (which reads core directly) before dropping the line view it uses.
            migrationBuilder.Sql(SqlResource.Read("V005_Marketplaces", "rpt.vw_SettlementSummary.sql"));
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt.vw_SettlementLine;");
        }
    }
}
