using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the date of the AWD snapshot the AWD columns come from to <c>rpt.vw_InventoryPosition</c>,
    /// so the Inventory page can say how fresh its AWD stock is.
    /// </summary>
    public partial class AddAwdSnapshotDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("V018_AwdSnapshotDate", "rpt.vw_InventoryPosition.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("V016_PendingToAmazon", "rpt.vw_InventoryPosition.sql"));
        }
    }
}
