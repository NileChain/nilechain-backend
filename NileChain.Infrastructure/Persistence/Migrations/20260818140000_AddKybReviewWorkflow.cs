using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NileChain.Infrastructure.Persistence;

#nullable disable

namespace NileChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(NileChainDbContext))]
    [Migration("20260818140000_AddKybReviewWorkflow")]
    public partial class AddKybReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KybAdminNote",
                table: "AspNetUsers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KybReviewedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KybReviewedByUserId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KybReviewStatus",
                table: "AspNetUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "Recommendation",
                table: "KybVerificationReport",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NeedsReview");

            migrationBuilder.CreateTable(
                name: "KybDecision",
                columns: table => new
                {
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TrustScoreAtDecision = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KybDecision", x => x.DecisionId);
                    table.ForeignKey(
                        name: "FK_KybDecision_AspNetUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KybDecision_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KybDecision_CreatedAt",
                table: "KybDecision",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_KybDecision_UserId",
                table: "KybDecision",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_KybDecision_AdminUserId",
                table: "KybDecision",
                column: "AdminUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KybDecision");

            migrationBuilder.DropColumn(
                name: "Recommendation",
                table: "KybVerificationReport");

            migrationBuilder.DropColumn(
                name: "KybAdminNote",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "KybReviewedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "KybReviewedByUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "KybReviewStatus",
                table: "AspNetUsers");
        }
    }
}
