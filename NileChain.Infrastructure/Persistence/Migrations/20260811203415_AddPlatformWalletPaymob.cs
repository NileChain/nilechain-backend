using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformWalletPaymob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EscrowTransactions_IdempotencyKey",
                table: "EscrowTransactions");

            migrationBuilder.AddColumn<Guid>(
                name: "FundingLedgerEntryId",
                table: "EscrowTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymobOrderId",
                table: "EscrowTransactions",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymobTransactionId",
                table: "EscrowTransactions",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvailableBalanceEgp = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    HeldBalanceEgp = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.WalletId);
                });

            migrationBuilder.CreateTable(
                name: "WalletLedgerEntries",
                columns: table => new
                {
                    LedgerEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AmountEgp = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    AvailableAfterEgp = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    HeldAfterEgp = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReferenceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletLedgerEntries", x => x.LedgerEntryId);
                    table.ForeignKey(
                        name: "FK_WalletLedgerEntries_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "WalletId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WalletTopUps",
                columns: table => new
                {
                    TopUpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountEgp = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymobIntentionId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PaymobOrderId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PaymobTransactionId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ClientSecret = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTopUps", x => x.TopUpId);
                    table.ForeignKey(
                        name: "FK_WalletTopUps_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "WalletId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WalletWithdrawals",
                columns: table => new
                {
                    WithdrawalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountEgp = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DestinationSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FailReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletWithdrawals", x => x.WithdrawalId);
                    table.ForeignKey(
                        name: "FK_WalletWithdrawals_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "WalletId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EscrowTransactions_IdempotencyKey",
                table: "EscrowTransactions",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_WalletLedgerEntries_WalletId",
                table: "WalletLedgerEntries",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_OwnerType_OwnerId",
                table: "Wallets",
                columns: new[] { "OwnerType", "OwnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUps_IdempotencyKey",
                table: "WalletTopUps",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUps_PaymobTransactionId",
                table: "WalletTopUps",
                column: "PaymobTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTopUps_WalletId",
                table: "WalletTopUps",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletWithdrawals_WalletId",
                table: "WalletWithdrawals",
                column: "WalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WalletLedgerEntries");

            migrationBuilder.DropTable(
                name: "WalletTopUps");

            migrationBuilder.DropTable(
                name: "WalletWithdrawals");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_EscrowTransactions_IdempotencyKey",
                table: "EscrowTransactions");

            migrationBuilder.DropColumn(
                name: "FundingLedgerEntryId",
                table: "EscrowTransactions");

            migrationBuilder.DropColumn(
                name: "PaymobOrderId",
                table: "EscrowTransactions");

            migrationBuilder.DropColumn(
                name: "PaymobTransactionId",
                table: "EscrowTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowTransactions_IdempotencyKey",
                table: "EscrowTransactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }
    }
}
