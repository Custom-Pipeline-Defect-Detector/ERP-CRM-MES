using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingPlatform.Host.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationAdapterExecutionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "IntegrationJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DispatchEndpoint",
                table: "IntegrationJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalBatchNumber",
                table: "IntegrationJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalJobNumber",
                table: "IntegrationJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptedAt",
                table: "IntegrationJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastMessage",
                table: "IntegrationJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestPayloadJson",
                table: "IntegrationJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseCode",
                table: "IntegrationJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsePayloadJson",
                table: "IntegrationJobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountingExportPath",
                table: "ExternalSystems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AdapterMode",
                table: "ExternalSystems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "JobStatusPathTemplate",
                table: "ExternalSystems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "ExternalSystems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "AccountingExportBatches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExternalBatchNumber",
                table: "AccountingExportBatches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastMessage",
                table: "AccountingExportBatches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationJobs_ExternalJobNumber",
                table: "IntegrationJobs",
                column: "ExternalJobNumber");

            migrationBuilder.Sql(
                """
                UPDATE ExternalSystems
                SET
                    AdapterMode = 0,
                    AccountingExportPath = '/accounting-export-batches',
                    JobStatusPathTemplate = '/jobs/{externalJobNumber}',
                    BaseUrl = CASE
                        WHEN BaseUrl = 'https://api123.icu/v1' THEN 'mock://kingdee-finance'
                        ELSE BaseUrl
                    END
                WHERE SystemCode = 'KINGDEE';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationJobs_ExternalJobNumber",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "DispatchEndpoint",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "ExternalBatchNumber",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "ExternalJobNumber",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "LastAttemptedAt",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "LastMessage",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "RequestPayloadJson",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "ResponseCode",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "ResponsePayloadJson",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "AccountingExportPath",
                table: "ExternalSystems");

            migrationBuilder.DropColumn(
                name: "AdapterMode",
                table: "ExternalSystems");

            migrationBuilder.DropColumn(
                name: "JobStatusPathTemplate",
                table: "ExternalSystems");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "ExternalSystems");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "AccountingExportBatches");

            migrationBuilder.DropColumn(
                name: "ExternalBatchNumber",
                table: "AccountingExportBatches");

            migrationBuilder.DropColumn(
                name: "LastMessage",
                table: "AccountingExportBatches");
        }
    }
}
