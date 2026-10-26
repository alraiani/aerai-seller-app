using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Breaks the settlement Refunds column into refunded sales and refunded expenses, and Expenses into
    /// shipping, FBA fees, commission, and other expenses, and adds the advertising Amazon charged. Only
    /// the three settlement views change; the bucket columns stay as they were and no data moves.
    /// </summary>
    public partial class BreakOutSettlementExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The line view first: the two summaries read its new columns.
            migrationBuilder.Sql(SqlResource.Read("V021_SettlementExpenseBreakdown", "rpt.vw_SettlementLine.sql"));
            migrationBuilder.Sql(SqlResource.Read("V021_SettlementExpenseBreakdown", "rpt.vw_SettlementSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V021_SettlementExpenseBreakdown", "rpt.vw_SettlementSkuSummary.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The summaries first: the V019 line view no longer has the columns the V021 summaries read.
            migrationBuilder.Sql(SqlResource.Read("V019_AmazonStatementColumns", "rpt.vw_SettlementSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V019_AmazonStatementColumns", "rpt.vw_SettlementSkuSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V019_AmazonStatementColumns", "rpt.vw_SettlementLine.sql"));
        }
    }
}
