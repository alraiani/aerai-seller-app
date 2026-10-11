using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleActiveHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "ActiveFrom",
                schema: "ops",
                table: "SyncSchedule",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ActiveUntil",
                schema: "ops",
                table: "SyncSchedule",
                type: "time",
                nullable: true);

            // Orders now pull every 4 hours instead of hourly: hourly pulls kept the serverless
            // database from ever auto-pausing, and fresher data is a "Run now" away. Seeded names
            // ("... Orders — hourly") follow, unless that would clash with another active schedule.
            migrationBuilder.Sql("""
                UPDATE ops.SyncSchedule
                SET IntervalMinutes = 240
                WHERE ReportType = N'Orders' AND Frequency = N'Interval' AND IntervalMinutes < 240 AND DeletedAt IS NULL;

                UPDATE s
                SET Name = REPLACE(s.Name, N'— hourly', N'— every 4 h')
                FROM ops.SyncSchedule AS s
                WHERE s.ReportType = N'Orders' AND s.Frequency = N'Interval' AND s.IntervalMinutes = 240
                  AND s.DeletedAt IS NULL AND s.Name LIKE N'%— hourly'
                  AND NOT EXISTS (
                      SELECT 1 FROM ops.SyncSchedule AS o
                      WHERE o.DeletedAt IS NULL AND o.Name = REPLACE(s.Name, N'— hourly', N'— every 4 h'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The 4-hour Orders interval is a settings change, not schema: Down leaves it (and the
            // renamed schedules) as they are; change them back on the Amazon sync page if needed.
            migrationBuilder.DropColumn(
                name: "ActiveFrom",
                schema: "ops",
                table: "SyncSchedule");

            migrationBuilder.DropColumn(
                name: "ActiveUntil",
                schema: "ops",
                table: "SyncSchedule");
        }
    }
}
