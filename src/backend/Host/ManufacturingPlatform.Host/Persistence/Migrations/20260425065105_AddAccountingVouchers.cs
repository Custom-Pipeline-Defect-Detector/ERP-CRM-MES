using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingPlatform.Host.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VoucherNumber = table.Column<string>(type: "TEXT", nullable: false),
                    PeriodCode = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalDebitAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalCreditAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    LineCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PostedBy = table.Column<string>(type: "TEXT", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingVouchers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountingVoucherLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountingVoucherId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountingBridgeEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceBridgeNumber = table.Column<string>(type: "TEXT", nullable: false),
                    SourceDocumentNumber = table.Column<string>(type: "TEXT", nullable: false),
                    SourceEvent = table.Column<string>(type: "TEXT", nullable: false),
                    AccountCode = table.Column<string>(type: "TEXT", nullable: false),
                    DebitAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreditAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Narrative = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingVoucherLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountingVoucherLines_AccountingBridgeEntries_AccountingBridgeEntryId",
                        column: x => x.AccountingBridgeEntryId,
                        principalTable: "AccountingBridgeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingVoucherLines_AccountingVouchers_AccountingVoucherId",
                        column: x => x.AccountingVoucherId,
                        principalTable: "AccountingVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingVoucherLines_AccountingBridgeEntryId",
                table: "AccountingVoucherLines",
                column: "AccountingBridgeEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingVoucherLines_AccountingVoucherId_LineNumber",
                table: "AccountingVoucherLines",
                columns: new[] { "AccountingVoucherId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingVouchers_PeriodCode",
                table: "AccountingVouchers",
                column: "PeriodCode");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingVouchers_VoucherNumber",
                table: "AccountingVouchers",
                column: "VoucherNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingVoucherLines");

            migrationBuilder.DropTable(
                name: "AccountingVouchers");
        }
    }
}
