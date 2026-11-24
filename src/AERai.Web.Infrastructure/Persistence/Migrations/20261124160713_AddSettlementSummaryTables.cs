using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Works the settlement pages' numbers out once, when a settlement is promoted, instead of on every page
    /// read: adds the rpt settlement summary tables and their refresh proc, has the promotion proc call it,
    /// and rewrites the three settlement views over the tables with the same columns. The next migration,
    /// <c>BackfillSettlementSummaryTables</c>, fills them for the settlements already imported.
    /// </summary>
    public partial class AddSettlementSummaryTables : Migration
    {
        internal const string Folder = "V024_SettlementSummaryTables";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read(Folder, "tables.sql"));
            migrationBuilder.Sql(SqlResource.Read(Folder, "rpt.usp_RefreshSettlementTotals.sql"));
            migrationBuilder.Sql(SqlResource.Read(Folder, "core.usp_PromoteImportBatch.sql"));
            migrationBuilder.Sql(SqlResource.Read(Folder, "rpt.vw_SettlementLine.sql"));
            migrationBuilder.Sql(SqlResource.Read(Folder, "rpt.vw_SettlementSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read(Folder, "rpt.vw_SettlementSkuSummary.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The V021 views and V022 proc first: the V024 versions read the tables dropped below.
            migrationBuilder.Sql(SqlResource.Read("V021_SettlementExpenseBreakdown", "rpt.vw_SettlementLine.sql"));
            migrationBuilder.Sql(SqlResource.Read("V021_SettlementExpenseBreakdown", "rpt.vw_SettlementSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V021_SettlementExpenseBreakdown", "rpt.vw_SettlementSkuSummary.sql"));
            migrationBuilder.Sql(SqlResource.Read("V022_InventoryHealth", "core.usp_PromoteImportBatch.sql"));
            migrationBuilder.Sql(SqlResource.Read(Folder, "down.sql"));
        }
    }
}
