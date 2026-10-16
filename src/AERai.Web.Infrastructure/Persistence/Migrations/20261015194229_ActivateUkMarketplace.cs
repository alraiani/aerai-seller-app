using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Activates the UK marketplace now that the Europe region's SP-API authorization
    /// (<c>SpApi:Europe:RefreshToken</c>) exists. Its default schedules are seeded, disabled,
    /// by the next app start.
    /// </summary>
    public partial class ActivateUkMarketplace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "core",
                table: "Marketplace",
                keyColumn: "MarketplaceId",
                keyValue: "A1F83G8C2ARO7P",
                column: "IsActive",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "core",
                table: "Marketplace",
                keyColumn: "MarketplaceId",
                keyValue: "A1F83G8C2ARO7P",
                column: "IsActive",
                value: false);
        }
    }
}
