using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NileChain.Infrastructure.Persistence;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations;

/// <summary>
/// Applies AgentRun / IdempotencyKey / RowVersion that were in the model snapshot
/// but never reached the database (prior hand-written migration lacked a Designer
/// and was not discovered by EF).
/// </summary>
[DbContext(typeof(NileChainDbContext))]
[Migration("20260811123000_AddAgentRunIdempotencyRowVersion")]
public partial class AddAgentRunIdempotencyRowVersion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "IdempotencyKey",
            table: "SupplyRequest",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SupplyRequest_FactoryId_IdempotencyKey",
            table: "SupplyRequest",
            columns: new[] { "FactoryId", "IdempotencyKey" },
            unique: true,
            filter: "[IdempotencyKey] IS NOT NULL");

        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "Contract",
            type: "rowversion",
            rowVersion: true,
            nullable: false);

        migrationBuilder.CreateTable(
            name: "AgentRun",
            columns: table => new
            {
                RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FactoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Success = table.Column<bool>(type: "bit", nullable: false),
                ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                TruncatedCount = table.Column<int>(type: "int", nullable: true),
                OrchestratorMode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AgentRun", x => x.RunId);
                table.ForeignKey(
                    name: "FK_AgentRun_SupplyRequest_RequestId",
                    column: x => x.RequestId,
                    principalTable: "SupplyRequest",
                    principalColumn: "RequestId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AgentRun_RequestId",
            table: "AgentRun",
            column: "RequestId");

        migrationBuilder.CreateIndex(
            name: "IX_AgentRun_StartedAt",
            table: "AgentRun",
            column: "StartedAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AgentRun");

        migrationBuilder.DropIndex(
            name: "IX_SupplyRequest_FactoryId_IdempotencyKey",
            table: "SupplyRequest");

        migrationBuilder.DropColumn(
            name: "IdempotencyKey",
            table: "SupplyRequest");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "Contract");
    }
}
