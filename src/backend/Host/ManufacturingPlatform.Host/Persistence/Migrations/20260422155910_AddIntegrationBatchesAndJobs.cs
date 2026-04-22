using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingPlatform.Host.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationBatchesAndJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExportBatchId",
                table: "AccountingBridgeEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountingExportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BatchNumber = table.Column<string>(type: "TEXT", nullable: false),
                    TargetSystemCode = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    BridgeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingExportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalSystems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SystemCode = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    HealthStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalSystems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobNumber = table.Column<string>(type: "TEXT", nullable: false),
                    TargetSystemCode = table.Column<string>(type: "TEXT", nullable: false),
                    JobType = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    ExportBatchId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PayloadSummary = table.Column<string>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationJobs_AccountingExportBatches_ExportBatchId",
                        column: x => x.ExportBatchId,
                        principalTable: "AccountingExportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingBridgeEntries_ExportBatchId",
                table: "AccountingBridgeEntries",
                column: "ExportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingExportBatches_BatchNumber",
                table: "AccountingExportBatches",
                column: "BatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalSystems_SystemCode",
                table: "ExternalSystems",
                column: "SystemCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationJobs_ExportBatchId",
                table: "IntegrationJobs",
                column: "ExportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationJobs_JobNumber",
                table: "IntegrationJobs",
                column: "JobNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingBridgeEntries_AccountingExportBatches_ExportBatchId",
                table: "AccountingBridgeEntries",
                column: "ExportBatchId",
                principalTable: "AccountingExportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountingBridgeEntries_AccountingExportBatches_ExportBatchId",
                table: "AccountingBridgeEntries");

            migrationBuilder.DropTable(
                name: "ExternalSystems");

            migrationBuilder.DropTable(
                name: "IntegrationJobs");

            migrationBuilder.DropTable(
                name: "AccountingExportBatches");

            migrationBuilder.DropIndex(
                name: "IX_AccountingBridgeEntries_ExportBatchId",
                table: "AccountingBridgeEntries");

            migrationBuilder.DropColumn(
                name: "ExportBatchId",
                table: "AccountingBridgeEntries");
        }
    }
}
