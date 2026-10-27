using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyRestockRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderMinimum",
                schema: "core",
                table: "ProductFamily",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderMultiple",
                schema: "core",
                table: "ProductFamily",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SendLargeFrom",
                schema: "core",
                table: "ProductFamily",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SendMultiple",
                schema: "core",
                table: "ProductFamily",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SendMultipleLarge",
                schema: "core",
                table: "ProductFamily",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductFamily_RestockRules",
                schema: "core",
                table: "ProductFamily",
                sql: "(SendMultiple IS NULL OR SendMultiple > 0) AND (SendMultipleLarge IS NULL OR SendMultipleLarge > 0) AND (SendLargeFrom IS NULL OR SendLargeFrom > 0) AND (OrderMinimum IS NULL OR OrderMinimum > 0) AND (OrderMultiple IS NULL OR OrderMultiple > 0)");

            // Handkerchiefs go to Amazon in 5s (10s from 50 up) and are ordered from the supplier in batches of
            // at least 100, in 10s. Only families that have no rules yet are touched, so it is safe to rerun.
            migrationBuilder.Sql("""
                UPDATE core.ProductFamily
                SET SendMultiple = 5, SendMultipleLarge = 10, SendLargeFrom = 50, OrderMinimum = 100, OrderMultiple = 10
                WHERE Name LIKE N'%handkerchief%'
                  AND SendMultiple IS NULL AND SendMultipleLarge IS NULL AND SendLargeFrom IS NULL AND OrderMinimum IS NULL AND OrderMultiple IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductFamily_RestockRules",
                schema: "core",
                table: "ProductFamily");

            migrationBuilder.DropColumn(
                name: "OrderMinimum",
                schema: "core",
                table: "ProductFamily");

            migrationBuilder.DropColumn(
                name: "OrderMultiple",
                schema: "core",
                table: "ProductFamily");

            migrationBuilder.DropColumn(
                name: "SendLargeFrom",
                schema: "core",
                table: "ProductFamily");

            migrationBuilder.DropColumn(
                name: "SendMultiple",
                schema: "core",
                table: "ProductFamily");

            migrationBuilder.DropColumn(
                name: "SendMultipleLarge",
                schema: "core",
                table: "ProductFamily");
        }
    }
}
