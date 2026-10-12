using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixSettlementBatchDataRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settlements are now ranged by their period, not by the earliest posted line; recompute them all.
            migrationBuilder.Sql(SqlResource.Read("V012_BatchDataRangeSettlementPeriod", "stg.usp_SetBatchDataRange.sql"));
            migrationBuilder.Sql("EXEC stg.usp_SetBatchDataRange;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("V011_BatchDataRange", "stg.usp_SetBatchDataRange.sql"));
            migrationBuilder.Sql("EXEC stg.usp_SetBatchDataRange;");
        }
    }
}
