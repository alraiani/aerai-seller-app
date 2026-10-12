using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchDataRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataEnd",
                schema: "stg",
                table: "ImportBatch",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DataStart",
                schema: "stg",
                table: "ImportBatch",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql(SqlResource.Read("V011_BatchDataRange", "stg.usp_SetBatchDataRange.sql"));

            // Fill in the range for every batch staged before this migration, from the rows already in stg.
            migrationBuilder.Sql("EXEC stg.usp_SetBatchDataRange;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS stg.usp_SetBatchDataRange;");

            migrationBuilder.DropColumn(
                name: "DataEnd",
                schema: "stg",
                table: "ImportBatch");

            migrationBuilder.DropColumn(
                name: "DataStart",
                schema: "stg",
                table: "ImportBatch");
        }
    }
}
