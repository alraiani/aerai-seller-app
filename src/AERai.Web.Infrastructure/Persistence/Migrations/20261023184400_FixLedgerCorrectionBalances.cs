using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Makes ledger rows edited before the copy-first balance fix show the count after the edit, by swapping
    /// BalanceAfter between such a correction's copy and its reversal. Data only; no schema change.
    /// </summary>
    public partial class FixLedgerCorrectionBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("V020_LedgerCorrectionBalances", "fix-correction-balances.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: putting the in-between balances back on the visible rows would only bring the wrong numbers back.
        }
    }
}
