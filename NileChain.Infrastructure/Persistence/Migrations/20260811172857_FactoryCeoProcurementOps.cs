using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FactoryCeoProcurementOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiptFileName",
                table: "Transactions",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptPublicId",
                table: "Transactions",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptUploadedAt",
                table: "Transactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptUrl",
                table: "Transactions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SpecsMet",
                table: "Fulfillment",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecsOutcomeNotes",
                table: "Fulfillment",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptFileName",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReceiptPublicId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReceiptUploadedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReceiptUrl",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SpecsMet",
                table: "Fulfillment");

            migrationBuilder.DropColumn(
                name: "SpecsOutcomeNotes",
                table: "Fulfillment");
        }
    }
}
