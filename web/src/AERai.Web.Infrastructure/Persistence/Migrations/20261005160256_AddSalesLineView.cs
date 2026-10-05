using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds rpt.vw_SalesLine (one row per sold order item with its exact purchase time), which the
    /// dashboard aggregates by the business's local day and hour.
    /// </summary>
    public partial class AddSalesLineView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("V004_SalesLineView", "rpt.vw_SalesLine.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt.vw_SalesLine;");
        }
    }
}
