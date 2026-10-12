using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Fills order-item-code and quantity-purchased for settlements imported before
    /// <c>AddSettlementUnits</c>, from the raw lines kept in staging, and re-promotes them.
    /// Data only; no schema change.
    /// </summary>
    public partial class BackfillSettlementUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The promotion procedure runs its own transaction per batch, so this script must not be wrapped in the migration's.
            migrationBuilder.Sql(SqlResource.Read("V015_BackfillSettlementUnits", "backfill-settlement-units.sql"), suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the filled columns are harmless, and AddSettlementUnits drops them.
        }
    }
}
