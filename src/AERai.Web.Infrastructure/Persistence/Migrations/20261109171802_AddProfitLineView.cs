using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfitLineView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Profit & loss by posted date: the ledger, with settlement lines for the days before it began.
            migrationBuilder.Sql(SqlResource.Read("V023_ProfitLine", "rpt.vw_ProfitLine.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt.vw_ProfitLine;");
        }
    }
}
