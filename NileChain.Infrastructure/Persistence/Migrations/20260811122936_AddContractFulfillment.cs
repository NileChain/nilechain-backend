using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContractFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Fulfillment",
                columns: table => new
                {
                    FulfillmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PlannedShipDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QualityCheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FulfilledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QualityNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fulfillment", x => x.FulfillmentId);
                    table.ForeignKey(
                        name: "FK_Fulfillment_Contract_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contract",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FulfillmentEvent",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FulfillmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FulfillmentEvent", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_FulfillmentEvent_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FulfillmentEvent_Fulfillment_FulfillmentId",
                        column: x => x.FulfillmentId,
                        principalTable: "Fulfillment",
                        principalColumn: "FulfillmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fulfillment_ContractId",
                table: "Fulfillment",
                column: "ContractId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fulfillment_Status_PlannedShipDate",
                table: "Fulfillment",
                columns: new[] { "Status", "PlannedShipDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentEvent_ActorUserId",
                table: "FulfillmentEvent",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FulfillmentEvent_FulfillmentId",
                table: "FulfillmentEvent",
                column: "FulfillmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FulfillmentEvent");

            migrationBuilder.DropTable(
                name: "Fulfillment");
        }
    }
}
