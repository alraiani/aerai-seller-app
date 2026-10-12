using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncPageUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SyncSchedule_Name",
                schema: "ops",
                table: "SyncSchedule");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "ops",
                table: "SyncSchedule",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "ops",
                table: "SyncSchedule",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "ops",
                table: "SyncSchedule",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerEmail",
                schema: "ops",
                table: "SyncSchedule",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SyncSettings",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    IsPaused = table.Column<bool>(type: "bit", nullable: false),
                    PausedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PausedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncSettings", x => x.Id);
                    table.CheckConstraint("CK_SyncSettings_Singleton", "[Id] = 1");
                });

            migrationBuilder.InsertData(
                schema: "ops",
                table: "SyncSettings",
                columns: new[] { "Id", "IsPaused", "PausedAt", "PausedBy" },
                values: new object[] { 1, false, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_SyncSchedule_Name",
                schema: "ops",
                table: "SyncSchedule",
                column: "Name",
                unique: true,
                filter: "[DeletedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Without DeletedAt, deleted schedules would reappear and could clash with the restored
            // unconditional unique name index. Keep them off and give them a distinct name.
            migrationBuilder.Sql(@"
UPDATE ops.SyncSchedule
SET IsEnabled = 0,
    NextRunAt = NULL,
    Name = LEFT(Name, 70) + N' (deleted #' + CAST(Id AS nvarchar(10)) + N')'
WHERE DeletedAt IS NOT NULL;");

            migrationBuilder.DropTable(
                name: "SyncSettings",
                schema: "ops");

            migrationBuilder.DropIndex(
                name: "IX_SyncSchedule_Name",
                schema: "ops",
                table: "SyncSchedule");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "ops",
                table: "SyncSchedule");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "ops",
                table: "SyncSchedule");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "ops",
                table: "SyncSchedule");

            migrationBuilder.DropColumn(
                name: "OwnerEmail",
                schema: "ops",
                table: "SyncSchedule");

            migrationBuilder.CreateIndex(
                name: "IX_SyncSchedule_Name",
                schema: "ops",
                table: "SyncSchedule",
                column: "Name",
                unique: true);
        }
    }
}
