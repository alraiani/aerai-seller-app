using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Regroups settlement amounts into the columns of Seller Central's statement (Sales, Refunds,
    /// Expenses, Account level reserve) so every settlement page matches what Amazon shows. Only the
    /// three settlement views change; no data moves.
    /// </summary>
    public partial class MatchAmazonStatementColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The line view first: the two summaries read its new columns.
            migrationBuilder.Sql(SqlResource.Read("V019_AmazonStatementColumns", "rpt.vw_SettlementLine.sql"));
            migrationBuilder.Sql(SqlResource.Read("V019_AmazonStatementColumns", "rpt.vw_SettlementSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V019_AmazonStatementColumns", "rpt.vw_SettlementSkuSummary.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("V017_SettlementExchangeRates", "rpt.vw_SettlementLine.sql"));
            migrationBuilder.Sql(SqlResource.Read("V017_SettlementExchangeRates", "rpt.vw_SettlementSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V017_SettlementExchangeRates", "rpt.vw_SettlementSkuSummary.sql"));
        }
    }
}
