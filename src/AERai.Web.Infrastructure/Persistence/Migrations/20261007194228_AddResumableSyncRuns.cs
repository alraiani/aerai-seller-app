using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResumableSyncRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BackfillEnd",
                schema: "ops",
                table: "SyncRun",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BackfillStart",
                schema: "ops",
                table: "SyncRun",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextCheckAt",
                schema: "ops",
                table: "SyncRun",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingReportId",
                schema: "ops",
                table: "SyncRun",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PollAttempts",
                schema: "ops",
                table: "SyncRun",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SyncRun_NextCheckAt",
                schema: "ops",
                table: "SyncRun",
                column: "NextCheckAt",
                filter: "[PendingReportId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncRun_NextCheckAt",
                schema: "ops",
                table: "SyncRun");

            migrationBuilder.DropColumn(
                name: "BackfillEnd",
                schema: "ops",
                table: "SyncRun");

            migrationBuilder.DropColumn(
                name: "BackfillStart",
                schema: "ops",
                table: "SyncRun");

            migrationBuilder.DropColumn(
                name: "NextCheckAt",
                schema: "ops",
                table: "SyncRun");

            migrationBuilder.DropColumn(
                name: "PendingReportId",
                schema: "ops",
                table: "SyncRun");

            migrationBuilder.DropColumn(
                name: "PollAttempts",
                schema: "ops",
                table: "SyncRun");
        }
    }
}
