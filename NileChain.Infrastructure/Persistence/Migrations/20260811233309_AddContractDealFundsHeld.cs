using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContractDealFundsHeld : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FundsHeldAt",
                table: "Contract",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FundsHeldEgp",
                table: "Contract",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FundsHeldAt",
                table: "Contract");

            migrationBuilder.DropColumn(
                name: "FundsHeldEgp",
                table: "Contract");
        }
    }
}
