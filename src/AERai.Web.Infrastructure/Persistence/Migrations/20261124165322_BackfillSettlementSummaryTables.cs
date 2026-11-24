using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Fills the settlement summary tables (added by <c>AddSettlementSummaryTables</c>) for every settlement
    /// already imported. On its own because it runs outside a transaction.
    /// </summary>
    public partial class BackfillSettlementSummaryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One settlement per call, each committing on its own: a single transaction over every settlement
            // line would grow the log and hold locks for the whole backfill. The script is repeatable, so a
            // run cut short is finished by running it again.
            migrationBuilder.Sql(SqlResource.Read(AddSettlementSummaryTables.Folder, "backfill.sql"), suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the rows are derived, and AddSettlementSummaryTables' Down drops their tables.
        }
    }
}
