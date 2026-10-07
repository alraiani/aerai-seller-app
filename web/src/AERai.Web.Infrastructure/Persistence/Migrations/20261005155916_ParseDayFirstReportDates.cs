using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Teaches stg.ufn_TryParseDateTimeOffset day-first dates ("24.08.2026 01:50:50 UTC"), used by
    /// Canadian and Mexican settlement reports, which were previously rejected or misread month-first.
    /// Schema is unchanged; re-promote affected settlement batches to correct stored periods.
    /// </summary>
    public partial class ParseDayFirstReportDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("V003_DayFirstDates", "stg.ufn_TryParseDateTimeOffset.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlResource.Read("V001_Initial", "stg.ufn_TryParseDateTimeOffset.sql"));
        }
    }
}
