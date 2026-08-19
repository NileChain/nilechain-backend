using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NileChain.Infrastructure.Persistence;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NileChainDbContext))]
    [Migration("20260818190000_AddAgentRunTelemetry")]
    public partial class AddAgentRunTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMs",
                table: "AgentRun",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LlmProviders",
                table: "AgentRun",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LlmModels",
                table: "AgentRun",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LlmCalls",
                table: "AgentRun",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LlmLatencyMs",
                table: "AgentRun",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PromptTokens",
                table: "AgentRun",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletionTokens",
                table: "AgentRun",
                type: "int",
                nullable: true);

            // Nullable on purpose: an unpriced model records no cost instead of a fabricated one.
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCostUsd",
                table: "AgentRun",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EstimatedCostUsd", table: "AgentRun");
            migrationBuilder.DropColumn(name: "CompletionTokens", table: "AgentRun");
            migrationBuilder.DropColumn(name: "PromptTokens", table: "AgentRun");
            migrationBuilder.DropColumn(name: "LlmLatencyMs", table: "AgentRun");
            migrationBuilder.DropColumn(name: "LlmCalls", table: "AgentRun");
            migrationBuilder.DropColumn(name: "LlmModels", table: "AgentRun");
            migrationBuilder.DropColumn(name: "LlmProviders", table: "AgentRun");
            migrationBuilder.DropColumn(name: "DurationMs", table: "AgentRun");
        }
    }
}
