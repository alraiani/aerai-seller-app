using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// SP-API scheduled ingestion: the ops schema (schedules, run history, ingested-report ledger),
    /// the stg.FbaInventoryRow staging table, and V002 of the promotion procedure (adds FbaInventory).
    /// </summary>
    public partial class AddSpApiIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.CreateTable(
                name: "FbaInventoryRow",
                schema: "stg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sku = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Asin = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AfnFulfillableQuantity = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AfnUnsellableQuantity = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AfnReservedQuantity = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AfnInboundWorkingQuantity = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AfnInboundShippedQuantity = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AfnInboundReceivingQuantity = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    RawLine = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FbaInventoryRow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FbaInventoryRow_ImportBatch_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "stg",
                        principalTable: "ImportBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SyncSchedule",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IntervalMinutes = table.Column<int>(type: "int", nullable: true),
                    DailyTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LookbackDays = table.Column<int>(type: "int", nullable: false),
                    AutoPromote = table.Column<bool>(type: "bit", nullable: false),
                    NextRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSuccessfulDataEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncSchedule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncRun",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SyncScheduleId = table.Column<int>(type: "int", nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DataStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DataEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AmazonReportIds = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImportBatchIds = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRun", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncRun_SyncSchedule_SyncScheduleId",
                        column: x => x.SyncScheduleId,
                        principalSchema: "ops",
                        principalTable: "SyncSchedule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngestedReport",
                schema: "ops",
                columns: table => new
                {
                    AmazonReportId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    SyncRunId = table.Column<long>(type: "bigint", nullable: false),
                    IngestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestedReport", x => x.AmazonReportId);
                    table.ForeignKey(
                        name: "FK_IngestedReport_ImportBatch_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "stg",
                        principalTable: "ImportBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngestedReport_SyncRun_SyncRunId",
                        column: x => x.SyncRunId,
                        principalSchema: "ops",
                        principalTable: "SyncRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FbaInventoryRow_ImportBatchId_RowNumber",
                schema: "stg",
                table: "FbaInventoryRow",
                columns: new[] { "ImportBatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestedReport_ImportBatchId",
                schema: "ops",
                table: "IngestedReport",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_IngestedReport_SyncRunId",
                schema: "ops",
                table: "IngestedReport",
                column: "SyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncRun_SyncScheduleId_StartedAt",
                schema: "ops",
                table: "SyncRun",
                columns: new[] { "SyncScheduleId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncSchedule_IsEnabled_NextRunAt",
                schema: "ops",
                table: "SyncSchedule",
                columns: new[] { "IsEnabled", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncSchedule_Name",
                schema: "ops",
                table: "SyncSchedule",
                column: "Name",
                unique: true);

            migrationBuilder.Sql(SqlResource.Read("V002_SpApiIngestion", "core.usp_PromoteImportBatch.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the previous procedure version before dropping the table it no longer needs.
            migrationBuilder.Sql(SqlResource.Read("V001_Initial", "core.usp_PromoteImportBatch.sql"));

            migrationBuilder.DropTable(
                name: "FbaInventoryRow",
                schema: "stg");

            migrationBuilder.DropTable(
                name: "IngestedReport",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "SyncRun",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "SyncSchedule",
                schema: "ops");
        }
    }
}
