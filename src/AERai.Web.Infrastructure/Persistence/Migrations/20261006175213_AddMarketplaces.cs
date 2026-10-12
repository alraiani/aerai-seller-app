using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds core.Marketplace with the US, Canada, and UK marketplaces (UK inactive until EU SP-API
    /// credentials exist), so data and pages can be scoped to one marketplace at a time.
    /// </summary>
    public partial class AddMarketplaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Marketplace",
                schema: "core",
                columns: table => new
                {
                    MarketplaceId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SalesChannel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marketplace", x => x.MarketplaceId);
                });

            migrationBuilder.InsertData(
                schema: "core",
                table: "Marketplace",
                columns: new[] { "MarketplaceId", "Code", "Currency", "IsActive", "Name", "Region", "SalesChannel", "SortOrder", "TimeZoneId" },
                values: new object[,]
                {
                    { "A1F83G8C2ARO7P", "UK", "GBP", false, "United Kingdom", "Europe", "Amazon.co.uk", 3, "Europe/London" },
                    { "A2EUQ1WTGCTBG2", "CA", "CAD", true, "Canada", "NorthAmerica", "Amazon.ca", 2, "America/Toronto" },
                    { "ATVPDKIKX0DER", "US", "USD", true, "United States", "NorthAmerica", "Amazon.com", 1, "America/New_York" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Marketplace_Code",
                schema: "core",
                table: "Marketplace",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Marketplace",
                schema: "core");
        }
    }
}
