using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FarmCeoMarketplaceOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedQuantityTons",
                table: "Fulfillment",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Carrier",
                table: "Fulfillment",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "Fulfillment",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ShippedNotes",
                table: "Fulfillment",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingNumber",
                table: "Fulfillment",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountHolderName",
                table: "Farm",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "Farm",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Farm",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Iban",
                table: "Farm",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedQuantityTons",
                table: "Fulfillment");

            migrationBuilder.DropColumn(
                name: "Carrier",
                table: "Fulfillment");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "Fulfillment");

            migrationBuilder.DropColumn(
                name: "ShippedNotes",
                table: "Fulfillment");

            migrationBuilder.DropColumn(
                name: "TrackingNumber",
                table: "Fulfillment");

            migrationBuilder.DropColumn(
                name: "AccountHolderName",
                table: "Farm");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "Farm");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Farm");

            migrationBuilder.DropColumn(
                name: "Iban",
                table: "Farm");
        }
    }
}
