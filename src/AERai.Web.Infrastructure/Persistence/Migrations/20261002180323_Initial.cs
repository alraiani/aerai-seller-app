using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AERai.Web.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Initial schema: Identity (auth), staging (stg), curated (core) tables, plus the promotion
    /// procedure, helper functions, and reporting views (rpt).
    /// </summary>
    public partial class Initial : Migration
    {
        /// <summary>Folder under Persistence/Sql holding this migration's scripts.</summary>
        private const string ScriptFolder = "V001_Initial";

        /// <summary>Scripts in dependency order: functions, then the procedure that uses them, then views.</summary>
        private static readonly string[] UpScripts =
        [
            "stg.ufn_TryParseDateTimeOffset.sql",
            "stg.ufn_NormalizeInventoryState.sql",
            "core.usp_PromoteImportBatch.sql",
            "rpt.vw_DailySalesBySku.sql",
            "rpt.vw_OrderSummary.sql",
            "rpt.vw_InventoryPosition.sql",
            "rpt.vw_SettlementSummary.sql",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "stg");

            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.CreateTable(
                name: "ImportBatch",
                schema: "stg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    PromotedRowCount = table.Column<int>(type: "int", nullable: false),
                    RejectedRowCount = table.Column<int>(type: "int", nullable: false),
                    PromotedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Order",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AmazonOrderId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PurchaseDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PurchaseDateUtc = table.Column<DateOnly>(type: "date", nullable: false, computedColumnSql: "CAST(SWITCHOFFSET([PurchaseDate], '+00:00') AS date)", stored: true),
                    OrderStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    LastImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Order", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Product",
                schema: "core",
                columns: table => new
                {
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Asin = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CostOfGoods = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Product", x => x.Sku);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settlement",
                schema: "core",
                columns: table => new
                {
                    SettlementId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    LastImportBatchId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settlement", x => x.SettlementId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryRow",
                schema: "stg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotDate = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Sku = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Asin = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    State = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Quantity = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    RawLine = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryRow", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryRow_ImportBatch_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "stg",
                        principalTable: "ImportBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderLine",
                schema: "stg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AmazonOrderId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PurchaseDate = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    OrderStatus = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Sku = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Asin = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Quantity = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ItemPrice = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    RawLine = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLine_ImportBatch_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "stg",
                        principalTable: "ImportBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SettlementLine",
                schema: "stg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettlementId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SettlementStartDate = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SettlementEndDate = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PostedDate = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    OrderId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Sku = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AmountType = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AmountDescription = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    RawLine = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementLine_ImportBatch_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "stg",
                        principalTable: "ImportBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventorySnapshot",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    LastImportBatchId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventorySnapshot", x => x.Id);
                    table.CheckConstraint("CK_InventorySnapshot_State", "[State] IN (N'Available', N'Inbound', N'Reserved', N'Unfulfillable')");
                    table.ForeignKey(
                        name: "FK_InventorySnapshot_Product_Sku",
                        column: x => x.Sku,
                        principalSchema: "core",
                        principalTable: "Product",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItem",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ItemPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItem_Order_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "core",
                        principalTable: "Order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItem_Product_Sku",
                        column: x => x.Sku,
                        principalSchema: "core",
                        principalTable: "Product",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SettlementLine",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettlementId = table.Column<string>(type: "nvarchar(32)", nullable: false),
                    PostedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AmazonOrderId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AmountType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AmountDescription = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementLine_Settlement_SettlementId",
                        column: x => x.SettlementId,
                        principalSchema: "core",
                        principalTable: "Settlement",
                        principalColumn: "SettlementId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                schema: "auth",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "auth",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                schema: "auth",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatch_UploadedAt",
                schema: "stg",
                table: "ImportBatch",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRow_ImportBatchId_RowNumber",
                schema: "stg",
                table: "InventoryRow",
                columns: new[] { "ImportBatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventorySnapshot_Sku_SnapshotDate_State",
                schema: "core",
                table: "InventorySnapshot",
                columns: new[] { "Sku", "SnapshotDate", "State" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Order_AmazonOrderId",
                schema: "core",
                table: "Order",
                column: "AmazonOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Order_PurchaseDateUtc",
                schema: "core",
                table: "Order",
                column: "PurchaseDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItem_OrderId_Sku",
                schema: "core",
                table: "OrderItem",
                columns: new[] { "OrderId", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItem_Sku",
                schema: "core",
                table: "OrderItem",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLine_ImportBatchId_RowNumber",
                schema: "stg",
                table: "OrderLine",
                columns: new[] { "ImportBatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                schema: "auth",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "auth",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementLine_SettlementId",
                schema: "core",
                table: "SettlementLine",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementLine_ImportBatchId_RowNumber",
                schema: "stg",
                table: "SettlementLine",
                columns: new[] { "ImportBatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                schema: "auth",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                schema: "auth",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "auth",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "auth",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "auth",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            // Programmable objects and views, applied after the tables they depend on.
            // Scripts are versioned under Persistence/Sql/V001_Initial and must not change once applied.
            migrationBuilder.EnsureSchema(name: "rpt");
            foreach (var script in UpScripts)
            {
                migrationBuilder.Sql(SqlResource.Read(ScriptFolder, script));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop dependents first: views and the procedure reference the tables dropped below.
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt.vw_SettlementSummary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt.vw_InventoryPosition;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt.vw_OrderSummary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt.vw_DailySalesBySku;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS core.usp_PromoteImportBatch;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS stg.ufn_NormalizeInventoryState;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS stg.ufn_TryParseDateTimeOffset;");
            migrationBuilder.Sql("DROP SCHEMA IF EXISTS rpt;");

            migrationBuilder.DropTable(
                name: "InventoryRow",
                schema: "stg");

            migrationBuilder.DropTable(
                name: "InventorySnapshot",
                schema: "core");

            migrationBuilder.DropTable(
                name: "OrderItem",
                schema: "core");

            migrationBuilder.DropTable(
                name: "OrderLine",
                schema: "stg");

            migrationBuilder.DropTable(
                name: "RoleClaims",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "SettlementLine",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SettlementLine",
                schema: "stg");

            migrationBuilder.DropTable(
                name: "UserClaims",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UserLogins",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UserTokens",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Order",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Product",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Settlement",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ImportBatch",
                schema: "stg");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "auth");
        }
    }
}
