using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMilestoneTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_ContractId",
                table: "Transactions");

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "Transactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Percent",
                table: "Transactions",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                table: "Transactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleGeneration",
                table: "Transactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "Transactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "Transactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransactionEvents",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionEvents", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_TransactionEvents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionEvents_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "TransactionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ContractId_ScheduleGeneration_Sequence",
                table: "Transactions",
                columns: new[] { "ContractId", "ScheduleGeneration", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionEvents_ActorUserId",
                table: "TransactionEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionEvents_TransactionId",
                table: "TransactionEvents",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionEvents");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ContractId_ScheduleGeneration_Sequence",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Percent",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ScheduleGeneration",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ContractId",
                table: "Transactions",
                column: "ContractId");
        }
    }
}
