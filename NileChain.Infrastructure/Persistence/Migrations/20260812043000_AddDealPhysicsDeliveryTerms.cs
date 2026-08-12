using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NileChain.Infrastructure.Persistence;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(NileChainDbContext))]
    [Migration("20260812043000_AddDealPhysicsDeliveryTerms")]
    public partial class AddDealPhysicsDeliveryTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryPoint",
                table: "SupplyRequest",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FactoryGate");

            migrationBuilder.AddColumn<string>(
                name: "FreightPayer",
                table: "SupplyRequest",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Farm");

            migrationBuilder.AddColumn<string>(
                name: "TransitRisk",
                table: "SupplyRequest",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Farm");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPoint",
                table: "Fulfillment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "FactoryGate");

            migrationBuilder.AddColumn<string>(
                name: "FreightPayer",
                table: "Fulfillment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Farm");

            migrationBuilder.AddColumn<string>(
                name: "TransitRisk",
                table: "Fulfillment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Farm");

            migrationBuilder.AddColumn<decimal>(
                name: "WeighedQuantityTons",
                table: "Fulfillment",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeighbridgeTicketUrl",
                table: "Fulfillment",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAtGateAt",
                table: "Fulfillment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GateRejectReason",
                table: "Fulfillment",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GateRejectNotes",
                table: "Fulfillment",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnFreightBearer",
                table: "Fulfillment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DeliveryPoint", table: "SupplyRequest");
            migrationBuilder.DropColumn(name: "FreightPayer", table: "SupplyRequest");
            migrationBuilder.DropColumn(name: "TransitRisk", table: "SupplyRequest");

            migrationBuilder.DropColumn(name: "DeliveryPoint", table: "Fulfillment");
            migrationBuilder.DropColumn(name: "FreightPayer", table: "Fulfillment");
            migrationBuilder.DropColumn(name: "TransitRisk", table: "Fulfillment");
            migrationBuilder.DropColumn(name: "WeighedQuantityTons", table: "Fulfillment");
            migrationBuilder.DropColumn(name: "WeighbridgeTicketUrl", table: "Fulfillment");
            migrationBuilder.DropColumn(name: "RejectedAtGateAt", table: "Fulfillment");
            migrationBuilder.DropColumn(name: "GateRejectReason", table: "Fulfillment");
            migrationBuilder.DropColumn(name: "GateRejectNotes", table: "Fulfillment");
            migrationBuilder.DropColumn(name: "ReturnFreightBearer", table: "Fulfillment");
        }
    }
}
